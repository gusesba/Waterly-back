using System.Data;
using Microsoft.EntityFrameworkCore;
using Water.Application.Competition;
using Water.Domain.Competition;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Competition;

public sealed class ContestService(WaterDbContext dbContext, TimeProvider timeProvider) : IContestService
{
    public async Task<IReadOnlyCollection<ContestResponse>> GetAllAsync(string userId, CancellationToken token)
    {
        var contests = await dbContext.Contests.AsNoTracking().OrderBy(item => item.StartsOn).ThenBy(item => item.Name).ToArrayAsync(token);
        return await MapAsync(contests, userId, token);
    }

    public async Task<ContestResponse> GetAsync(string userId, Guid contestId, CancellationToken token)
    {
        var contest = await dbContext.Contests.AsNoTracking().SingleOrDefaultAsync(item => item.Id == contestId, token) ?? throw new ContestNotFoundException();
        return (await MapAsync([contest], userId, token)).Single();
    }

    public async Task<ContestResponse> JoinAsync(string userId, Guid contestId, Guid clientOperationId, CancellationToken token)
    {
        if (clientOperationId == Guid.Empty) throw new ContestValidationException();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var contest = await dbContext.Contests.SingleOrDefaultAsync(item => item.Id == contestId, token) ?? throw new ContestNotFoundException();
        var existing = await dbContext.ContestParticipants.SingleOrDefaultAsync(item => item.ContestId == contestId && item.UserId == userId, token);
        if (existing is null)
        {
            if (contest.EndsOn <= Today()) throw new ContestConflictException();
            dbContext.ContestParticipants.Add(new ContestParticipant(contestId, userId, clientOperationId, timeProvider.GetUtcNow()));
            await dbContext.SaveChangesAsync(token);
        }
        await transaction.CommitAsync(token);
        return (await MapAsync([contest], userId, token)).Single();
    }

    private async Task<IReadOnlyCollection<ContestResponse>> MapAsync(Contest[] contests, string userId, CancellationToken token)
    {
        var ids = contests.Select(item => item.Id).ToArray();
        var participants = await dbContext.ContestParticipants.AsNoTracking().Where(item => ids.Contains(item.ContestId)).ToArrayAsync(token);
        var today = Today();
        return contests.Select(item => new ContestResponse(
            item.Id, item.Name, item.StartsOn, item.EndsOn, item.DurationDays,
            item.ScoringRuleVersion, item.DailyScoreCap, item.Status(today),
            participants.Count(participant => participant.ContestId == item.Id),
            participants.Any(participant => participant.ContestId == item.Id && participant.UserId == userId))).ToArray();
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}
