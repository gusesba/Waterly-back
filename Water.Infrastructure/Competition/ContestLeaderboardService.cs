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
        foreach (var score in pageRows.Select(item => item.TotalScore).Distinct())
            positions[score] = await participants.CountAsync(item => item.TotalScore > score, token) + 1;
        var entries = pageRows.Select(item => new ContestLeaderboardEntryResponse(
            positions[item.TotalScore],
            item.Username,
            item.DisplayName,
            item.TotalScore,
            item.ScoredDays,
            item.UserId == userId)).ToArray();
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var hasProvisionalScore = await dbContext.ContestDailyScores.AsNoTracking()
            .AnyAsync(item => item.ContestId == contestId && !item.IsFinal, token);

        return new ContestLeaderboardResponse(entries, totalCount, page, pageSize,
            contest.Status(today) == "ended" && !hasProvisionalScore);
    }
}
