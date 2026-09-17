using System.Data;
using Microsoft.EntityFrameworkCore;
using Water.Application.Competition;
using Water.Domain.Competition;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Competition;

public sealed class ContestFinalizationService(
    WaterDbContext dbContext,
    IContestScoreService scoreService,
    TimeProvider timeProvider) : IContestFinalizationService
{
    public async Task<IReadOnlyCollection<Guid>> GetDueAsync(int batchSize, CancellationToken token)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return await dbContext.Contests.AsNoTracking()
            .Where(contest => contest.EndsOn <= today && !dbContext.ContestFinalizations.Any(item => item.ContestId == contest.Id))
            .OrderBy(contest => contest.EndsOn)
            .ThenBy(contest => contest.Id)
            .Select(contest => contest.Id)
            .Take(batchSize)
            .ToArrayAsync(token);
    }

    public async Task<bool> FinalizeAsync(Guid contestId, CancellationToken token)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        if (await dbContext.ContestFinalizations.AnyAsync(item => item.ContestId == contestId, token))
        {
            await transaction.CommitAsync(token);
            return true;
        }

        var contest = await dbContext.Contests.SingleOrDefaultAsync(item => item.Id == contestId, token)
            ?? throw new ContestNotFoundException();
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (contest.EndsOn > today) return false;

        var participants = await dbContext.ContestParticipants.AsNoTracking()
            .Where(item => item.ContestId == contestId)
            .OrderBy(item => item.UserId)
            .ToArrayAsync(token);
        var userIds = participants.Select(item => item.UserId).ToArray();
        var timeZones = await dbContext.Profiles.AsNoTracking()
            .Where(item => userIds.Contains(item.UserId))
            .ToDictionaryAsync(item => item.UserId, item => item.TimeZone, token);
        if (participants.Any(participant => !timeZones.TryGetValue(participant.UserId, out var zone) || LocalToday(zone) < contest.EndsOn))
            return false;

        await scoreService.RefreshContestAsync(contestId, token);
        var profiles = await dbContext.PublicProfiles.AsNoTracking()
            .Where(item => userIds.Contains(item.UserId))
            .ToDictionaryAsync(item => item.UserId, token);
        var totals = participants.Select(participant => new
        {
            participant.UserId,
            TotalScore = dbContext.ContestDailyScores.Where(score => score.ContestId == contestId && score.UserId == participant.UserId).Sum(score => score.Score),
            ScoredDays = dbContext.ContestDailyScores.Count(score => score.ContestId == contestId && score.UserId == participant.UserId)
        }).OrderByDescending(item => item.TotalScore).ThenBy(item => item.UserId).ToArray();
        var now = timeProvider.GetUtcNow();
        foreach (var item in totals)
        {
            profiles.TryGetValue(item.UserId, out var profile);
            var tiedCount = totals.Count(other => other.TotalScore == item.TotalScore);
            dbContext.ContestResults.Add(new ContestResult(
                contestId,
                item.UserId,
                totals.Count(other => other.TotalScore > item.TotalScore) + 1,
                item.TotalScore,
                item.ScoredDays,
                tiedCount > 1,
                profile?.Username,
                profile?.DisplayName,
                now));
        }
        dbContext.ContestFinalizations.Add(new ContestFinalization(contestId, contest.ScoringRuleVersion, participants.Length, now));
        await dbContext.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return true;
    }

    private DateOnly LocalToday(string timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), ResolveTimeZone(timeZone)).Date);

    private static TimeZoneInfo ResolveTimeZone(string value)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(value); }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new ArgumentException("Time zone is invalid.", nameof(value), exception); }
    }
}
