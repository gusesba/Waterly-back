using Microsoft.EntityFrameworkCore;
using Water.Application.Analytics;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Analytics;

public sealed class ProductMetricsService(WaterDbContext dbContext, TimeProvider timeProvider) : IProductMetricsService
{
    public async Task<ProductMetricsResponse> GetAsync(CancellationToken token)
    {
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var since = today.AddDays(-59);
        var retention = new List<RetentionMetricResponse>();
        foreach (var offset in new[] { 1, 7, 30 })
        {
            var cutoff = today.AddDays(-offset);
            var returnDate = dbContext.Database.IsNpgsql()
                ? "cohort.\"FirstDate\" + {2}" : "date(cohort.\"FirstDate\", '+' || {2} || ' days')";
            var sql = $$"""
                WITH first_days AS (
                    SELECT "UserId", MIN("LocalDate") AS "FirstDate"
                    FROM "DailyHydrations" WHERE "HydrationMl" > 0 GROUP BY "UserId"
                ), cohorts AS (
                    SELECT cohort."UserId", cohort."FirstDate",
                        CASE WHEN EXISTS (SELECT 1 FROM "GroupMemberships" AS member WHERE member."UserId" = cohort."UserId") THEN 1 ELSE 0 END AS "HasGroup",
                        CASE WHEN EXISTS (SELECT 1 FROM "DailyHydrations" AS daily WHERE daily."UserId" = cohort."UserId"
                            AND daily."HydrationMl" > 0 AND daily."LocalDate" = {{returnDate}}) THEN 1 ELSE 0 END AS "Retained"
                    FROM first_days AS cohort WHERE cohort."FirstDate" >= {0} AND cohort."FirstDate" <= {1}
                )
                SELECT "HasGroup", CAST(COUNT(*) AS INTEGER) AS "EligibleUsers",
                    CAST(SUM("Retained") AS INTEGER) AS "RetainedUsers"
                FROM cohorts GROUP BY "HasGroup"
                """;
            var rows = await dbContext.Database.SqlQueryRaw<RetentionRow>(sql, since, cutoff, offset).ToArrayAsync(token);
            foreach (var hasGroup in new[] { false, true })
            {
                var segment = rows.SingleOrDefault(row => row.HasGroup == (hasGroup ? 1 : 0));
                var eligible = segment?.EligibleUsers ?? 0;
                var retained = segment?.RetainedUsers ?? 0;
                retention.Add(new RetentionMetricResponse(offset, hasGroup ? "with-group" : "without-group",
                    eligible, retained, eligible == 0 ? null : Math.Round((decimal)retained / eligible, 4)));
            }
        }

        var accounts = await dbContext.Users.CountAsync(token);
        var groups = await dbContext.PrivateGroups.CountAsync(token);
        var friendships = await dbContext.Friendships.CountAsync(item => item.AcceptedAt != null, token);
        var inputMethods = await dbContext.DrinkEntries.GroupBy(item => item.InputMethod)
            .Select(group => new { Method = group.Key, Count = group.Count() }).ToArrayAsync(token);
        var drinks = inputMethods.Sum(item => item.Count);
        var reactions = await dbContext.FeedReactions.CountAsync(token);
        var participants = await dbContext.ContestParticipants.CountAsync(token);
        var completed = await dbContext.ContestFinalizations.CountAsync(token);
        var achievements = await dbContext.UserAchievements.CountAsync(token);
        var dropsEarned = await dbContext.DropsLedgerEntries.Where(item => item.Amount > 0).SumAsync(item => (long)item.Amount, token);
        var dropsSpent = -await dbContext.DropsLedgerEntries.Where(item => item.Amount < 0).SumAsync(item => (long)item.Amount, token);
        var prestige = await dbContext.PrestigeLedgerEntries.SumAsync(item => (long)item.Amount, token);
        var pending = await dbContext.PushNotificationMessages.CountAsync(item => item.CompletedAt == null, token);
        var failed = await dbContext.PushNotificationMessages.CountAsync(item => item.CompletedAt != null && item.LastError != null, token);
        var duration = dbContext.Database.IsNpgsql()
            ? "EXTRACT(EPOCH FROM (first.\"FirstAt\" - account.\"RegisteredAt\"))"
            : "(julianday(first.\"FirstAt\") - julianday(account.\"RegisteredAt\")) * 86400";
        var firstRecordSql = $$"""
            WITH first_records AS (
                SELECT "UserId", MIN("CreatedAt") AS "FirstAt" FROM "DrinkEntries" GROUP BY "UserId"
            )
            SELECT CAST(COUNT(*) AS INTEGER) AS "EligibleAccounts",
                CAST(AVG({{duration}}) AS DOUBLE PRECISION) AS "AverageSeconds"
            FROM first_records AS first INNER JOIN "AspNetUsers" AS account ON account."Id" = first."UserId"
            WHERE account."RegisteredAt" IS NOT NULL AND first."FirstAt" >= account."RegisteredAt"
            """;
        var firstRecord = (await dbContext.Database.SqlQueryRaw<FirstRecordRow>(firstRecordSql).ToArrayAsync(token)).Single();
        var quickAdd = inputMethods.SingleOrDefault(item => item.Method == "quick-add")?.Count ?? 0;
        var custom = inputMethods.SingleOrDefault(item => item.Method == "custom")?.Count ?? 0;
        var activeDays = await dbContext.DailyHydrations.CountAsync(item => item.HydrationMl > 0, token);
        var invites = await dbContext.GroupInvites.CountAsync(token);
        var previews = await dbContext.GroupInvites.SumAsync(item => item.PreviewCount, token);
        var accepts = await dbContext.GroupInvites.SumAsync(item => item.AcceptanceCount, token);
        var acquisition = new AcquisitionMetricsResponse(firstRecord.EligibleAccounts, firstRecord.AverageSeconds,
            quickAdd, custom, drinks - quickAdd - custom, activeDays == 0 ? null : Math.Round((decimal)drinks / activeDays, 4),
            invites, previews, accepts);
        var activeStreaks = await dbContext.UserStreaks.CountAsync(item => item.Current > 0, token);
        var longestStreak = await dbContext.UserStreaks.Select(item => (int?)item.Longest).MaxAsync(token) ?? 0;
        var dropsFlow = await dbContext.DropsLedgerEntries.GroupBy(item => new { item.EntryType, item.ReferenceType })
            .Select(group => new DropsFlowMetricResponse(group.Key.EntryType, group.Key.ReferenceType,
                group.Sum(item => item.Amount > 0 ? (long)item.Amount : 0L),
                -group.Sum(item => item.Amount < 0 ? (long)item.Amount : 0L))).ToArrayAsync(token);

        return new ProductMetricsResponse(now, since, "first-hydration-day;current-group-membership;local-civil-days",
            accounts, groups, friendships, drinks, reactions, participants, completed, achievements,
            dropsEarned, dropsSpent, prestige, pending, failed, retention, acquisition, activeStreaks, longestStreak, dropsFlow);
    }

    private sealed class RetentionRow
    {
        public int HasGroup { get; set; }
        public int EligibleUsers { get; set; }
        public int RetainedUsers { get; set; }
    }

    private sealed class FirstRecordRow
    {
        public int EligibleAccounts { get; set; }
        public double? AverageSeconds { get; set; }
    }
}
