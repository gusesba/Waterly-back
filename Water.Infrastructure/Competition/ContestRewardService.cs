using System.Data;
using Microsoft.EntityFrameworkCore;
using Water.Application.Competition;
using Water.Domain.Competition;
using Water.Domain.Feed;
using Water.Domain.Progression;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Competition;

public sealed class ContestRewardService(WaterDbContext dbContext, TimeProvider timeProvider) : IContestRewardService
{
    public async Task<IReadOnlyCollection<Guid>> GetDueAsync(int batchSize, CancellationToken token) =>
        await (from finalization in dbContext.ContestFinalizations.AsNoTracking()
               join contest in dbContext.Contests.AsNoTracking() on finalization.ContestId equals contest.Id
               where !dbContext.ContestRewardCheckpoints.Any(item => item.ContestId == finalization.ContestId)
               orderby contest.EndsOn, contest.Id
               select finalization.ContestId)
            .Take(batchSize)
            .ToArrayAsync(token);

    public async Task GrantAsync(Guid contestId, CancellationToken token)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        if (await dbContext.ContestRewardCheckpoints.AnyAsync(item => item.ContestId == contestId, token))
        {
            await transaction.CommitAsync(token);
            return;
        }

        var contest = await dbContext.Contests.SingleOrDefaultAsync(item => item.Id == contestId, token)
            ?? throw new ContestNotFoundException();
        if (!await dbContext.ContestFinalizations.AnyAsync(item => item.ContestId == contestId, token))
            throw new ContestConflictException();

        var definitions = await dbContext.ContestRewardDefinitions.AsNoTracking()
            .Where(item => item.RuleVersion == contest.RewardRuleVersion && item.DurationDays == contest.DurationDays)
            .ToDictionaryAsync(item => item.Position, token);
        if (!definitions.ContainsKey(0)) throw new InvalidOperationException("Contest reward policy is incomplete.");

        var results = await dbContext.ContestResults.AsNoTracking()
            .Where(item => item.ContestId == contestId && item.TotalScore > 0)
            .OrderBy(item => item.Position)
            .ThenBy(item => item.UserId)
            .ToArrayAsync(token);
        var now = timeProvider.GetUtcNow();
        var referenceId = contestId.ToString();
        var medal = new MedalDefinition(contestId, $"contest-{contestId:N}", contest.Name, contest.StartsOn, contest.EndsOn, contest.RewardRuleVersion, now);
        dbContext.MedalDefinitions.Add(medal);

        foreach (var result in results)
        {
            AddReward(result.UserId, definitions[0], "contest-participation", referenceId,
                $"contest:{contestId:N}:participation:v{contest.RewardRuleVersion}", now);
            if (result.Position is >= 1 and <= 3 && definitions.TryGetValue(result.Position, out var placement))
            {
                AddReward(result.UserId, placement, "contest-placement", referenceId,
                    $"contest:{contestId:N}:placement:{result.Position}:v{contest.RewardRuleVersion}", now);
                dbContext.UserMedals.Add(new UserMedal(medal.Id, result.UserId, result.Position, contest.RewardRuleVersion, now));
                dbContext.FeedEvents.Add(new FeedEvent(
                    result.UserId,
                    "contest-medal",
                    "friends",
                    referenceId,
                    contest.Name,
                    $"contest-medal:{contestId:N}:{result.UserId}:v{contest.RewardRuleVersion}",
                    now));
            }
        }

        dbContext.ContestRewardCheckpoints.Add(new ContestRewardCheckpoint(contestId, contest.RewardRuleVersion, now));
        await dbContext.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }

    private void AddReward(
        string userId,
        ContestRewardDefinition definition,
        string entryType,
        string referenceId,
        string idempotencyKey,
        DateTimeOffset createdAt)
    {
        if (definition.DropsReward > 0)
            dbContext.DropsLedgerEntries.Add(new DropsLedgerEntry(userId, definition.DropsReward, entryType, "contest", referenceId, idempotencyKey, definition.RuleVersion, createdAt));
        if (definition.PrestigeReward > 0)
            dbContext.PrestigeLedgerEntries.Add(new PrestigeLedgerEntry(userId, definition.PrestigeReward, entryType, "contest", referenceId, idempotencyKey, definition.RuleVersion, createdAt));
    }
}
