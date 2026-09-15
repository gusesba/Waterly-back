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
        var beverage = await GetBeverageAsync(request.BeverageCode, cancellationToken);
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
            request.TimeZone,
            beverage.Code,
            CalculateHydrationMl(request.VolumeMl, beverage.HydrationFactor));
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

    public async Task<TodayHydrationResponse> UpdateEntryAsync(
        string userId,
        Guid entryId,
        UpdateDrinkEntryRequest request,
        CancellationToken cancellationToken)
    {
        EnsureOperationId(request.ClientOperationId);
        var payload = $"{request.VolumeMl}:{request.BeverageCode}";
        if (await IsProcessedOperationAsync(
            userId, request.ClientOperationId, entryId, "update", payload, cancellationToken))
        {
            var processedContext = await GetTodayContextAsync(userId, cancellationToken);
            return await BuildTodayAsync(userId, processedContext, cancellationToken);
        }

        var entry = await dbContext.DrinkEntries.SingleOrDefaultAsync(
            item => (item.Id == entryId || item.ClientEntryId == entryId) && item.UserId == userId,
            cancellationToken) ?? throw new DrinkEntryNotFoundException();

        var beverage = await GetBeverageAsync(request.BeverageCode, cancellationToken);
        entry.Update(
            request.VolumeMl,
            beverage.Code,
            CalculateHydrationMl(request.VolumeMl, beverage.HydrationFactor));
        dbContext.HydrationOperations.Add(new HydrationOperation(
            userId, request.ClientOperationId, entryId, "update", payload));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            if (!await IsProcessedOperationAsync(
                userId, request.ClientOperationId, entryId, "update", payload, cancellationToken))
            {
                throw;
            }
        }

        var context = await GetTodayContextAsync(userId, cancellationToken);
        return await BuildTodayAsync(userId, context, cancellationToken);
    }

    public async Task<TodayHydrationResponse> DeleteEntryAsync(
        string userId,
        Guid entryId,
        Guid clientOperationId,
        CancellationToken cancellationToken)
    {
        EnsureOperationId(clientOperationId);
        if (await IsProcessedOperationAsync(
            userId, clientOperationId, entryId, "delete", string.Empty, cancellationToken))
        {
            var processedContext = await GetTodayContextAsync(userId, cancellationToken);
            return await BuildTodayAsync(userId, processedContext, cancellationToken);
        }

        var entry = await dbContext.DrinkEntries.SingleOrDefaultAsync(
            item => (item.Id == entryId || item.ClientEntryId == entryId) && item.UserId == userId,
            cancellationToken) ?? throw new DrinkEntryNotFoundException();

        dbContext.DrinkEntries.Remove(entry);
        dbContext.HydrationOperations.Add(new HydrationOperation(
            userId, clientOperationId, entryId, "delete", string.Empty));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            if (!await IsProcessedOperationAsync(
                userId, clientOperationId, entryId, "delete", string.Empty, cancellationToken))
            {
                throw;
            }
        }

        var context = await GetTodayContextAsync(userId, cancellationToken);
        return await BuildTodayAsync(userId, context, cancellationToken);
    }

    public async Task<HydrationHistoryResponse> GetHistoryAsync(
        string userId,
        int days,
        CancellationToken cancellationToken)
    {
        if (days is < 1 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(days));
        }

        var profile = await dbContext.Profiles
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken)
            ?? throw new HydrationProfileRequiredException();
        var timeZone = ResolveTimeZone(profile.TimeZone);
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone).Date);
        var firstDate = today.AddDays(-(days - 1));
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(
            firstDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
            timeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(
            today.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
            timeZone);
        var entries = await dbContext.DrinkEntries
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.OccurredAtUtc >= startUtc && item.OccurredAtUtc < endUtc)
            .OrderByDescending(item => item.OccurredAtUtc)
            .ToArrayAsync(cancellationToken);
        var goals = await dbContext.HydrationGoals
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.EffectiveFrom <= today)
            .OrderByDescending(item => item.EffectiveFrom)
            .ToArrayAsync(cancellationToken);

        var daysResponse = Enumerable.Range(0, days)
            .Select(offset => today.AddDays(-offset))
            .Where(date => goals.Any(item => item.EffectiveFrom <= date))
            .Select(date =>
        {
            var dailyEntries = entries
                .Where(item => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(item.OccurredAtUtc, timeZone)) == date)
                .Select(ToResponse)
                .ToArray();
            var target = goals.First(item => item.EffectiveFrom <= date).DailyTargetMl;
            var consumed = dailyEntries.Sum(item => item.HydrationMl);

            return new HydrationHistoryDayResponse(
                date,
                target,
                consumed,
                Math.Min((double)consumed / target, 1),
                dailyEntries);
        }).ToArray();

        return new HydrationHistoryResponse(daysResponse);
    }

    public async Task<IReadOnlyCollection<BeverageResponse>> GetBeveragesAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Beverages
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderByDescending(item => dbContext.DrinkEntries
                .Where(entry => entry.UserId == userId && entry.BeverageCode == item.Code)
                .Sum(entry => (int?)entry.VolumeMl) ?? 0)
            .ThenBy(item => item.SortOrder)
            .Select(item => new BeverageResponse(
                item.Code,
                item.HydrationFactor,
                dbContext.DrinkEntries
                    .Where(entry => entry.UserId == userId && entry.BeverageCode == item.Code)
                    .Sum(entry => (int?)entry.VolumeMl) ?? 0))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<QuickAddSuggestionResponse>> GetSuggestionsAsync(
        string userId,
        string beverageCode,
        CancellationToken cancellationToken)
    {
        _ = await GetBeverageAsync(beverageCode, cancellationToken);
        var learned = await dbContext.DrinkEntries
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.BeverageCode == beverageCode &&
                dbContext.Beverages.Any(beverage =>
                    beverage.Code == item.BeverageCode && beverage.IsActive))
            .GroupBy(item => new { item.BeverageCode, item.VolumeMl })
            .Select(group => new
            {
                group.Key.BeverageCode,
                group.Key.VolumeMl,
                Count = group.Count(),
                LastUsed = group.Max(item => item.OccurredAtUtc)
            })
            .OrderByDescending(item => item.Count)
            .ThenByDescending(item => item.LastUsed)
            .Take(3)
            .Select(item => new QuickAddSuggestionResponse(item.BeverageCode, item.VolumeMl))
            .ToArrayAsync(cancellationToken);

        QuickAddSuggestionResponse[] defaults =
        [
                new(beverageCode, 250),
                new(beverageCode, 350),
                new(beverageCode, 500)
        ];

        return learned
            .Concat(defaults)
            .DistinctBy(item => new { item.BeverageCode, item.VolumeMl })
            .Take(3)
            .ToArray();
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
        var consumedMl = entries.Sum(item => item.HydrationMl);

        return new TodayHydrationResponse(
            context.Date,
            context.DailyTargetMl,
            consumedMl,
            Math.Min((double)consumedMl / context.DailyTargetMl, 1),
            entries.Select(ToResponse).ToArray());
    }

    private static DrinkEntryResponse ToResponse(DrinkEntry item) => new(
        item.Id,
        item.ClientEntryId,
        item.VolumeMl,
        item.HydrationMl,
        item.BeverageCode,
        new DateTimeOffset(item.OccurredAtUtc, TimeSpan.Zero),
        item.TimeZone,
        item.Source);

    private static void EnsureMatchingRequest(
        DrinkEntry entry,
        AddDrinkEntryRequest request)
    {
        if (
            entry.VolumeMl != request.VolumeMl ||
            !string.Equals(entry.BeverageCode, request.BeverageCode, StringComparison.Ordinal) ||
            entry.OccurredAtUtc != request.OccurredAt.UtcDateTime ||
            !string.Equals(entry.TimeZone, request.TimeZone, StringComparison.Ordinal)
        )
        {
            throw new IdempotencyConflictException();
        }
    }

    private async Task<Beverage> GetBeverageAsync(
        string beverageCode,
        CancellationToken cancellationToken)
    {
        return await dbContext.Beverages
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Code == beverageCode && item.IsActive,
                cancellationToken) ?? throw new BeverageNotFoundException();
    }

    private static int CalculateHydrationMl(int volumeMl, decimal hydrationFactor) =>
        (int)Math.Round(volumeMl * hydrationFactor, MidpointRounding.AwayFromZero);

    private async Task<bool> IsProcessedOperationAsync(
        string userId,
        Guid clientOperationId,
        Guid entryId,
        string operationType,
        string payload,
        CancellationToken cancellationToken)
    {
        var operation = await dbContext.HydrationOperations
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.UserId == userId && item.ClientOperationId == clientOperationId,
                cancellationToken);

        if (operation is null)
        {
            return false;
        }

        if (operation.EntryId != entryId ||
            operation.OperationType != operationType ||
            operation.Payload != payload)
        {
            throw new IdempotencyConflictException();
        }

        return true;
    }

    private static void EnsureOperationId(Guid clientOperationId)
    {
        if (clientOperationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Client operation identifier is required.",
                nameof(clientOperationId));
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
