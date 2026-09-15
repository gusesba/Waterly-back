using Microsoft.EntityFrameworkCore;
using Water.Application.Habits;
using Water.Domain.Habits;
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
                unlocked.Add(definition.Code, achievement);
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
