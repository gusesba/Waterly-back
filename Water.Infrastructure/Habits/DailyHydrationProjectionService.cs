using Microsoft.EntityFrameworkCore;
using Water.Application.Habits;
using Water.Application.Hydration;
using Water.Domain.Habits;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Habits;

public sealed class DailyHydrationProjectionService(WaterDbContext dbContext, TimeProvider timeProvider) : IDailyHydrationProjectionService
{
    public async Task RebuildAsync(string userId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.Profiles.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken)
            ?? throw new HydrationProfileRequiredException();
        var profileTimeZone = ResolveTimeZone(profile.TimeZone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), profileTimeZone).Date);
        var goals = await dbContext.HydrationGoals.AsNoTracking().Where(item => item.UserId == userId && item.EffectiveFrom <= today).OrderBy(item => item.EffectiveFrom).ToArrayAsync(cancellationToken);
        if (goals.Length == 0) throw new HydrationProfileRequiredException();
        var entries = await dbContext.DrinkEntries.AsNoTracking().Where(item => item.UserId == userId).OrderBy(item => item.OccurredAtUtc).ToArrayAsync(cancellationToken);
        var groupedEntries = entries.GroupBy(item => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(item.OccurredAtUtc, ResolveTimeZone(item.TimeZone)))).Where(group => group.Key <= today).ToArray();
        var existingDays = await dbContext.DailyHydrations.Where(item => item.UserId == userId).ToDictionaryAsync(item => item.LocalDate, cancellationToken);
        var rebuiltDates = new HashSet<DateOnly>();

        foreach (var group in groupedEntries)
        {
            var goal = goals.LastOrDefault(item => item.EffectiveFrom <= group.Key);
            if (goal is null) continue;
            var hydrationMl = group.Sum(item => item.HydrationMl);
            var completedAt = FindCompletedAt(group, goal.DailyTargetMl);
            var timeZone = group.Last().TimeZone;
            if (existingDays.TryGetValue(group.Key, out var daily)) daily.Update(timeZone, goal.DailyTargetMl, hydrationMl, completedAt);
            else dbContext.DailyHydrations.Add(new DailyHydration(userId, group.Key, timeZone, goal.DailyTargetMl, hydrationMl, completedAt));
            rebuiltDates.Add(group.Key);
        }

        if (!rebuiltDates.Contains(today))
        {
            var todayGoal = goals.Last(item => item.EffectiveFrom <= today);
            if (existingDays.TryGetValue(today, out var todayDaily)) todayDaily.Update(profile.TimeZone, todayGoal.DailyTargetMl, 0, null);
            else dbContext.DailyHydrations.Add(new DailyHydration(userId, today, profile.TimeZone, todayGoal.DailyTargetMl, 0, null));
            rebuiltDates.Add(today);
        }

        dbContext.DailyHydrations.RemoveRange(existingDays.Values.Where(item => !rebuiltDates.Contains(item.LocalDate)));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static DateTimeOffset? FindCompletedAt(IEnumerable<Water.Domain.Hydration.DrinkEntry> entries, int target)
    {
        var total = 0;
        foreach (var entry in entries.OrderBy(item => item.OccurredAtUtc))
        {
            total += entry.HydrationMl;
            if (total >= target) return new DateTimeOffset(entry.OccurredAtUtc, TimeSpan.Zero);
        }
        return null;
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZone)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(timeZone); }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new ArgumentException("Time zone is invalid.", nameof(timeZone), exception); }
    }
}
