using Microsoft.EntityFrameworkCore;
using Water.Application.Social;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Social;

public sealed class GroupLeaderboardService(WaterDbContext dbContext, TimeProvider timeProvider) : IGroupLeaderboardService
{
    public async Task<GroupLeaderboardResponse> GetAsync(string userId, Guid groupId, int page, int pageSize, CancellationToken token)
    {
        if (page < 1 || page > int.MaxValue / 50 || pageSize is < 1 or > 50)
            throw new GroupLeaderboardValidationException();
        if (!await dbContext.GroupMemberships.AsNoTracking().AnyAsync(item => item.GroupId == groupId && item.UserId == userId, token))
            throw new GroupNotFoundException();

        var endsOn = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var startsOn = endsOn.AddDays(-6);
        var totalCount = await dbContext.GroupMemberships.AsNoTracking().CountAsync(item => item.GroupId == groupId, token);
        var offset = (page - 1) * pageSize;
        var rows = await dbContext.Database.SqlQuery<LeaderboardRow>($"""
            WITH scores AS (
                SELECT member."UserId", profile."Username", profile."DisplayName",
                       profile."NormalizedUsername" AS "SortName",
                       COALESCE(loadout."AuraCode", 'natural') AS "AuraCode",
                       COALESCE(SUM(ROUND(daily."Progress" * 100, 2)), 0.0) AS "TotalScore",
                       CAST(SUM(CASE WHEN daily."IsCompleted" THEN 1 ELSE 0 END) AS INTEGER) AS "CompletedDays"
                FROM "GroupMemberships" AS member
                LEFT JOIN "PublicProfiles" AS profile ON profile."UserId" = member."UserId"
                LEFT JOIN "CharacterLoadouts" AS loadout ON loadout."UserId" = member."UserId"
                LEFT JOIN "DailyHydrations" AS daily ON daily."UserId" = member."UserId"
                    AND daily."LocalDate" >= {startsOn} AND daily."LocalDate" <= {endsOn}
                WHERE member."GroupId" = {groupId}
                GROUP BY member."UserId", profile."Username", profile."DisplayName", profile."NormalizedUsername", loadout."AuraCode"
            ), ranked AS (
                SELECT *, CAST(RANK() OVER (ORDER BY "TotalScore" DESC) AS INTEGER) AS "Position",
                       CAST(COUNT(*) OVER (PARTITION BY "TotalScore") AS INTEGER) AS "TieCount"
                FROM scores
            )
            SELECT "UserId", "Username", "DisplayName", "AuraCode", "TotalScore", "CompletedDays", "Position", "TieCount"
            FROM ranked
            ORDER BY "TotalScore" DESC, COALESCE("SortName", "UserId"), "UserId"
            LIMIT {pageSize} OFFSET {offset}
            """).ToArrayAsync(token);

        return new GroupLeaderboardResponse(rows.Select(row => new GroupLeaderboardEntryResponse(
            row.Position, row.Username, row.DisplayName, row.AuraCode, row.TotalScore,
            row.CompletedDays, row.TieCount > 1, row.UserId == userId)).ToArray(),
            totalCount, page, pageSize, startsOn, endsOn, 1);
    }

    private sealed class LeaderboardRow
    {
        public string UserId { get; set; } = string.Empty;
        public string? Username { get; set; }
        public string? DisplayName { get; set; }
        public string AuraCode { get; set; } = "natural";
        public decimal TotalScore { get; set; }
        public int CompletedDays { get; set; }
        public int Position { get; set; }
        public int TieCount { get; set; }
    }
}
