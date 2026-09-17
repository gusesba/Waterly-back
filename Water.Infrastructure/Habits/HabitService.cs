using Microsoft.EntityFrameworkCore;
using Water.Application.Habits;
using Water.Application.Hydration;
using Water.Domain.Habits;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Habits;

public sealed class HabitService(
    WaterDbContext dbContext,
    IDailyHydrationProjectionService dailyHydrationProjectionService,
    TimeProvider timeProvider) : IHabitService
{
    public async Task<StreakResponse> GetStreakAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.Profiles
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken)
            ?? throw new HydrationProfileRequiredException();
        var profileTimeZone = ResolveTimeZone(profile.TimeZone);
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), profileTimeZone).Date);
        await dailyHydrationProjectionService.RebuildAsync(userId, cancellationToken);
        var completedDates = await dbContext.DailyHydrations.AsNoTracking()
            .Where(item => item.UserId == userId && item.LocalDate <= today && item.IsCompleted)
            .Select(item => item.LocalDate).ToArrayAsync(cancellationToken);
        var result = StreakCalculator.Calculate(completedDates, today);
        var streak = await dbContext.UserStreaks
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (streak is null)
        {
            streak = new UserStreak(userId);
            dbContext.UserStreaks.Add(streak);
        }
        streak.Update(result.Current, result.Longest, result.LastCompletedDate);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new StreakResponse(
            result.Current,
            result.Longest,
            completedDates.Contains(today),
            result.LastCompletedDate);
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException("Time zone is invalid.", nameof(timeZone), exception);
        }
    }
}
