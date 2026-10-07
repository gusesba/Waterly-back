using Microsoft.EntityFrameworkCore;
using Water.Application.Competition;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Competition;

public sealed class ContestLeaderboardService(
    WaterDbContext dbContext,
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

        var offset = (page - 1) * pageSize;
        var totalCount = await dbContext.ContestParticipants.AsNoTracking().CountAsync(item => item.ContestId == contestId, token);
        var pageRows = await dbContext.Database.SqlQuery<ProvisionalLeaderboardRow>($"""
            WITH scores AS (
                SELECT participant."UserId",
                       profile."Username",
                       profile."DisplayName",
                       profile."NormalizedUsername" AS "SortName",
                       COALESCE(SUM(score."Score"), 0.0) AS "TotalScore",
                       CAST(COUNT(score."Id") AS INTEGER) AS "ScoredDays"
                FROM "ContestParticipants" AS participant
                LEFT JOIN "PublicProfiles" AS profile ON profile."UserId" = participant."UserId"
                LEFT JOIN "ContestDailyScores" AS score
                    ON score."ContestId" = participant."ContestId" AND score."UserId" = participant."UserId"
                WHERE participant."ContestId" = {contestId}
                GROUP BY participant."UserId", profile."Username", profile."DisplayName", profile."NormalizedUsername"
            ), ranked AS (
                SELECT "UserId", "Username", "DisplayName", "SortName", "TotalScore", "ScoredDays",
                       CAST(RANK() OVER (ORDER BY "TotalScore" DESC) AS INTEGER) AS "Position",
                       CAST(COUNT(*) OVER (PARTITION BY "TotalScore") AS INTEGER) AS "TieCount"
                FROM scores
            )
            SELECT "UserId", "Username", "DisplayName", "TotalScore", "ScoredDays", "Position", "TieCount"
            FROM ranked
            ORDER BY "TotalScore" DESC, COALESCE("SortName", "UserId"), "UserId"
            LIMIT {pageSize} OFFSET {offset}
            """).ToArrayAsync(token);
        var entries = pageRows.Select(item => new ContestLeaderboardEntryResponse(
            item.Position,
            item.Username,
            item.DisplayName,
            item.TotalScore,
            item.ScoredDays,
            item.TieCount > 1,
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
            .ThenBy(item => item.Username ?? item.UserId ?? string.Empty)
            .ThenBy(item => item.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(token);
        var userIds = rows.Where(item => item.UserId is not null).Select(item => item.UserId!).ToArray();
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
            item.UserId is null ? 0 : drops.GetValueOrDefault(item.UserId),
            item.UserId is null ? 0 : prestige.GetValueOrDefault(item.UserId),
            item.UserId is not null && medalPositions.TryGetValue(item.UserId, out var medalPosition) ? medalPosition : null,
            item.UserId == userId)).ToArray(), totalCount, page, pageSize, true);
    }

    private sealed class ProvisionalLeaderboardRow
    {
        public required string UserId { get; init; }
        public string? Username { get; init; }
        public string? DisplayName { get; init; }
        public decimal TotalScore { get; init; }
        public int ScoredDays { get; init; }
        public int Position { get; init; }
        public int TieCount { get; init; }
    }
}
