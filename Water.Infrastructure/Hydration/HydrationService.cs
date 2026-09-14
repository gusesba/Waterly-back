using Microsoft.EntityFrameworkCore;
using Water.Application.Hydration;
using Water.Domain.Hydration;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Hydration;

public sealed class HydrationService(
    WaterDbContext dbContext,
    TimeProvider timeProvider) : IHydrationService
{
    public async Task<TodayHydrationResponse> GetTodayAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var context = await GetTodayContextAsync(userId, cancellationToken);
        return await BuildTodayAsync(userId, context, cancellationToken);
    }

    public async Task<TodayHydrationResponse> AddEntryAsync(
        string userId,
        AddDrinkEntryRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ClientEntryId == Guid.Empty)
        {
            throw new ArgumentException(
                "Client entry identifier is required.",
                nameof(AddDrinkEntryRequest.ClientEntryId));
        }

        ResolveTimeZone(request.TimeZone);
        var context = await GetTodayContextAsync(userId, cancellationToken);
        var existing = await dbContext.DrinkEntries.SingleOrDefaultAsync(
            item => item.UserId == userId && item.ClientEntryId == request.ClientEntryId,
            cancellationToken);

        if (existing is not null)
        {
            EnsureMatchingRequest(existing, request);
            return await BuildTodayAsync(userId, context, cancellationToken);
        }

        var entry = new DrinkEntry(
            userId,
            request.ClientEntryId,
            request.VolumeMl,
            request.OccurredAt,
            request.TimeZone);
        dbContext.DrinkEntries.Add(entry);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(entry).State = EntityState.Detached;
            var concurrentEntry = await dbContext.DrinkEntries.SingleOrDefaultAsync(
                item => item.UserId == userId && item.ClientEntryId == request.ClientEntryId,
                cancellationToken);

            if (concurrentEntry is null)
            {
                throw;
            }

            EnsureMatchingRequest(concurrentEntry, request);
        }

        return await BuildTodayAsync(userId, context, cancellationToken);
    }

    private async Task<TodayContext> GetTodayContextAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.Profiles
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken)
            ?? throw new HydrationProfileRequiredException();
        var timeZone = ResolveTimeZone(profile.TimeZone);
        var now = timeProvider.GetUtcNow();
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).Date);
        var goal = await dbContext.HydrationGoals
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.EffectiveFrom <= localDate)
            .OrderByDescending(item => item.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new HydrationProfileRequiredException();

        var localStart = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localEnd = localDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        return new TodayContext(
            localDate,
            goal.DailyTargetMl,
            TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone),
            TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone));
    }

    private async Task<TodayHydrationResponse> BuildTodayAsync(
        string userId,
        TodayContext context,
        CancellationToken cancellationToken)
    {
        var entries = await dbContext.DrinkEntries
            .AsNoTracking()
            .Where(item =>
                item.UserId == userId &&
                item.OccurredAtUtc >= context.StartUtc &&
                item.OccurredAtUtc < context.EndUtc)
            .OrderByDescending(item => item.OccurredAtUtc)
            .ToArrayAsync(cancellationToken);
        var consumedMl = entries.Sum(item => item.VolumeMl);

        return new TodayHydrationResponse(
            context.Date,
            context.DailyTargetMl,
            consumedMl,
            Math.Min((double)consumedMl / context.DailyTargetMl, 1),
            entries.Select(item => new DrinkEntryResponse(
                item.Id,
                item.ClientEntryId,
                item.VolumeMl,
                new DateTimeOffset(item.OccurredAtUtc, TimeSpan.Zero),
                item.TimeZone,
                item.Source)).ToArray());
    }

    private static void EnsureMatchingRequest(
        DrinkEntry entry,
        AddDrinkEntryRequest request)
    {
        if (
            entry.VolumeMl != request.VolumeMl ||
            entry.OccurredAtUtc != request.OccurredAt.UtcDateTime ||
            !string.Equals(entry.TimeZone, request.TimeZone, StringComparison.Ordinal)
        )
        {
            throw new IdempotencyConflictException();
        }
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new ArgumentException("Time zone is invalid.", "timeZone", exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new ArgumentException("Time zone is invalid.", "timeZone", exception);
        }
    }

    private sealed record TodayContext(
        DateOnly Date,
        int DailyTargetMl,
        DateTime StartUtc,
        DateTime EndUtc);
}
