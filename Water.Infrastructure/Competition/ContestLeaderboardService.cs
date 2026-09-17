using Microsoft.EntityFrameworkCore;
using Water.Application.Competition;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Competition;

public sealed class ContestLeaderboardService(
    WaterDbContext dbContext,
    IContestScoreService scoreService,
    TimeProvider timeProvider) : IContestLeaderboardService
{
    public async Task<ContestLeaderboardResponse> GetAsync(
        string userId,
        Guid contestId,
        int page,
        int pageSize,
        CancellationToken token)
    {
        if (page < 1 || pageSize is < 1 or > 50) throw new ContestValidationException();
        var contest = await dbContext.Contests.AsNoTracking().SingleOrDefaultAsync(item => item.Id == contestId, token)
            ?? throw new ContestNotFoundException();
        if (await dbContext.ContestFinalizations.AsNoTracking().AnyAsync(item => item.ContestId == contestId, token))
            return await GetFinalAsync(userId, contestId, page, pageSize, token);

        await scoreService.RefreshContestAsync(contestId, token);

        var participants = dbContext.ContestParticipants.AsNoTracking()
            .Where(item => item.ContestId == contestId)
            .Select(participant => new
            {
                participant.UserId,
                Username = dbContext.PublicProfiles.Where(profile => profile.UserId == participant.UserId).Select(profile => profile.Username).SingleOrDefault(),
                DisplayName = dbContext.PublicProfiles.Where(profile => profile.UserId == participant.UserId).Select(profile => profile.DisplayName).SingleOrDefault(),
                SortName = dbContext.PublicProfiles.Where(profile => profile.UserId == participant.UserId).Select(profile => profile.NormalizedUsername).SingleOrDefault(),
                TotalScore = dbContext.ContestDailyScores.Where(score => score.ContestId == contestId && score.UserId == participant.UserId).Sum(score => (decimal?)score.Score) ?? 0,
                ScoredDays = dbContext.ContestDailyScores.Count(score => score.ContestId == contestId && score.UserId == participant.UserId)
            });
        var totalCount = await participants.CountAsync(token);
        var offset = (page - 1) * pageSize;
        var pageRows = await participants
            .OrderByDescending(item => item.TotalScore)
            .ThenBy(item => item.SortName ?? item.UserId)
            .ThenBy(item => item.UserId)
            .Skip(offset)
            .Take(pageSize)
            .ToArrayAsync(token);

        var positions = new Dictionary<decimal, int>();
        var ties = new Dictionary<decimal, bool>();
        foreach (var score in pageRows.Select(item => item.TotalScore).Distinct())
        {
            positions[score] = await participants.CountAsync(item => item.TotalScore > score, token) + 1;
            ties[score] = await participants.CountAsync(item => item.TotalScore == score, token) > 1;
        }
        var entries = pageRows.Select(item => new ContestLeaderboardEntryResponse(
            positions[item.TotalScore],
            item.Username,
            item.DisplayName,
            item.TotalScore,
            item.ScoredDays,
            ties[item.TotalScore],
            0,
            0,
            null,
            item.UserId == userId)).ToArray();
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var hasProvisionalScore = await dbContext.ContestDailyScores.AsNoTracking()
            .AnyAsync(item => item.ContestId == contestId && !item.IsFinal, token);

        return new ContestLeaderboardResponse(entries, totalCount, page, pageSize,
            contest.Status(today) == "ended" && !hasProvisionalScore);
    }

    private async Task<ContestLeaderboardResponse> GetFinalAsync(
        string userId,
        Guid contestId,
        int page,
        int pageSize,
        CancellationToken token)
    {
        var results = dbContext.ContestResults.AsNoTracking().Where(item => item.ContestId == contestId);
        var totalCount = await results.CountAsync(token);
        var rows = await results.OrderBy(item => item.Position)
            .ThenBy(item => item.Username ?? item.UserId)
            .ThenBy(item => item.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(token);
        var userIds = rows.Select(item => item.UserId).ToArray();
        var referenceId = contestId.ToString();
        var drops = await dbContext.DropsLedgerEntries.AsNoTracking()
            .Where(item => userIds.Contains(item.UserId) && item.ReferenceType == "contest" && item.ReferenceId == referenceId)
            .GroupBy(item => item.UserId)
            .ToDictionaryAsync(group => group.Key, group => group.Sum(item => item.Amount), token);
        var prestige = await dbContext.PrestigeLedgerEntries.AsNoTracking()
            .Where(item => userIds.Contains(item.UserId) && item.ReferenceType == "contest" && item.ReferenceId == referenceId)
            .GroupBy(item => item.UserId)
            .ToDictionaryAsync(group => group.Key, group => group.Sum(item => item.Amount), token);
        var medalPositions = await (from userMedal in dbContext.UserMedals.AsNoTracking()
                                    join definition in dbContext.MedalDefinitions.AsNoTracking() on userMedal.MedalDefinitionId equals definition.Id
                                    where definition.ContestId == contestId && userIds.Contains(userMedal.UserId)
                                    select new { userMedal.UserId, userMedal.Position })
            .ToDictionaryAsync(item => item.UserId, item => item.Position, token);
        return new ContestLeaderboardResponse(rows.Select(item => new ContestLeaderboardEntryResponse(
            item.Position,
            item.Username,
            item.DisplayName,
            item.TotalScore,
            item.ScoredDays,
            item.IsTied,
            drops.GetValueOrDefault(item.UserId),
            prestige.GetValueOrDefault(item.UserId),
            medalPositions.TryGetValue(item.UserId, out var medalPosition) ? medalPosition : null,
            item.UserId == userId)).ToArray(), totalCount, page, pageSize, true);
    }
}
