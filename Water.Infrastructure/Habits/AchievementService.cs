using Microsoft.EntityFrameworkCore;
using Water.Application.Habits;
using Water.Domain.Habits;
using Water.Domain.Progression;
using Water.Domain.Feed;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Habits;

public sealed class AchievementService(
    WaterDbContext dbContext,
    IHabitService habitService,
    TimeProvider timeProvider) : IAchievementService
{
    public async Task<IReadOnlyCollection<AchievementResponse>> GetAchievementsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var streak = await habitService.GetStreakAsync(userId, cancellationToken);
        var completedDays = await dbContext.DailyHydrations
            .AsNoTracking()
            .CountAsync(item => item.UserId == userId && item.IsCompleted, cancellationToken);
        var definitions = await dbContext.AchievementDefinitions
            .AsNoTracking()
            .OrderBy(item => item.SortOrder)
            .ToArrayAsync(cancellationToken);
        var unlocked = await dbContext.UserAchievements
            .Where(item => item.UserId == userId)
            .ToDictionaryAsync(item => item.AchievementCode, cancellationToken);
        var dropsKeys = (await dbContext.DropsLedgerEntries
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.IdempotencyKey)
            .ToArrayAsync(cancellationToken)).ToHashSet();
        var prestigeKeys = (await dbContext.PrestigeLedgerEntries
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.IdempotencyKey)
            .ToArrayAsync(cancellationToken)).ToHashSet();

        foreach (var definition in definitions)
        {
            var progress = AchievementEvaluator.GetProgress(
                definition.Criterion, completedDays, streak.Longest);
            if (progress >= definition.Requirement && !unlocked.ContainsKey(definition.Code))
            {
                var achievement = new UserAchievement(
                    userId,
                    definition.Code,
                    timeProvider.GetUtcNow(),
                    definition.RuleVersion);
                dbContext.UserAchievements.Add(achievement);
                dbContext.FeedEvents.Add(new FeedEvent(
                    userId,
                    "achievement-unlocked",
                    "friends",
                    definition.Code,
                    definition.Code,
                    $"achievement-unlocked:{userId}:{definition.Code}:v{definition.RuleVersion}",
                    achievement.UnlockedAt));
                unlocked.Add(definition.Code, achievement);
            }

            if (!unlocked.TryGetValue(definition.Code, out var unlockedAchievement)) continue;
            var rewardKey = $"achievement:{definition.Code}:v{definition.RuleVersion}";
            if (definition.DropsReward > 0 && dropsKeys.Add(rewardKey))
            {
                dbContext.DropsLedgerEntries.Add(new DropsLedgerEntry(
                    userId,
                    definition.DropsReward,
                    "achievement-reward",
                    "achievement",
                    definition.Code,
                    rewardKey,
                    definition.RuleVersion,
                    unlockedAchievement.UnlockedAt));
            }
            if (definition.PrestigeReward > 0 && prestigeKeys.Add(rewardKey))
            {
                dbContext.PrestigeLedgerEntries.Add(new PrestigeLedgerEntry(
                    userId,
                    definition.PrestigeReward,
                    "achievement-reward",
                    "achievement",
                    definition.Code,
                    rewardKey,
                    definition.RuleVersion,
                    unlockedAchievement.UnlockedAt));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return definitions.Select(definition =>
        {
            unlocked.TryGetValue(definition.Code, out var achievement);
            var progress = achievement is not null
                ? definition.Requirement
                : Math.Min(
                    AchievementEvaluator.GetProgress(
                        definition.Criterion, completedDays, streak.Longest),
                    definition.Requirement);
            return new AchievementResponse(
                definition.Code,
                progress,
                definition.Requirement,
                achievement is not null,
                achievement?.UnlockedAt);
        }).ToArray();
    }

}
