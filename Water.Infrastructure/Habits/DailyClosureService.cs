using Microsoft.EntityFrameworkCore;
using Water.Application.Habits;
using Water.Domain.Habits;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Habits;

public sealed class DailyClosureService(
    WaterDbContext dbContext,
    IAchievementService achievementService,
    TimeProvider timeProvider) : IDailyClosureService
{
    public async Task<IReadOnlyCollection<DailyClosureCandidate>> GetDueAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        var profiles = await dbContext.Profiles
            .AsNoTracking()
            .OrderBy(item => item.UserId)
            .Select(item => new { item.UserId, item.TimeZone })
            .ToArrayAsync(cancellationToken);
        var checkpoints = await dbContext.DailyClosureCheckpoints
            .AsNoTracking()
            .ToDictionaryAsync(item => item.UserId, item => item.ClosedThrough, cancellationToken);
        var now = timeProvider.GetUtcNow();

        return profiles
            .Select(profile => new DailyClosureCandidate(
                profile.UserId,
                DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, ResolveTimeZone(profile.TimeZone)).Date).AddDays(-1)))
            .Where(candidate => !checkpoints.TryGetValue(candidate.UserId, out var closedThrough) || closedThrough < candidate.CloseThrough)
            .Take(batchSize)
            .ToArray();
    }

    public async Task CloseAsync(
        DailyClosureCandidate candidate,
        CancellationToken cancellationToken)
    {
        var checkpoint = await dbContext.DailyClosureCheckpoints
            .SingleOrDefaultAsync(item => item.UserId == candidate.UserId, cancellationToken);
        if (checkpoint is not null && checkpoint.ClosedThrough >= candidate.CloseThrough) return;

        await achievementService.GetAchievementsAsync(candidate.UserId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (checkpoint is null)
        {
            dbContext.DailyClosureCheckpoints.Add(new DailyClosureCheckpoint(
                candidate.UserId, candidate.CloseThrough, now));
        }
        else
        {
            checkpoint.AdvanceTo(candidate.CloseThrough, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException("Time zone is invalid.", nameof(timeZone), exception);
        }
    }
}
