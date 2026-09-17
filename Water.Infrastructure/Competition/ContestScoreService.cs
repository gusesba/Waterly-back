using Microsoft.EntityFrameworkCore;
using Water.Application.Competition;
using Water.Application.Habits;
using Water.Application.Hydration;
using Water.Domain.Competition;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Competition;

public sealed class ContestScoreService(WaterDbContext dbContext, IDailyHydrationProjectionService projectionService, TimeProvider timeProvider) : IContestScoreService
{
    public async Task<ContestScoreResponse> GetAsync(string userId, Guid contestId, CancellationToken token)
    {
        var contest = await dbContext.Contests.SingleOrDefaultAsync(item => item.Id == contestId, token) ?? throw new ContestNotFoundException();
        var participant = await dbContext.ContestParticipants.SingleOrDefaultAsync(item => item.ContestId == contestId && item.UserId == userId, token) ?? throw new ContestNotFoundException();
        var today = await LocalTodayAsync(userId, token);
        await RefreshAsync(contest, participant, today, today.AddDays(-1), token);
        return await MapAsync(contest, userId, token);
    }

    public async Task FinalizeThroughAsync(string userId, DateOnly closeThrough, CancellationToken token)
    {
        var participations = await dbContext.ContestParticipants.Where(item => item.UserId == userId).ToArrayAsync(token);
        if (participations.Length == 0) return;
        var ids = participations.Select(item => item.ContestId).ToArray();
        var contests = await dbContext.Contests.Where(item => ids.Contains(item.Id) && item.StartsOn <= closeThrough).ToDictionaryAsync(item => item.Id, token);
        foreach (var participant in participations)
            if (contests.TryGetValue(participant.ContestId, out var contest))
                await RefreshAsync(contest, participant, closeThrough, closeThrough, token);
    }

    private async Task RefreshAsync(Contest contest, ContestParticipant participant, DateOnly through, DateOnly finalizeThrough, CancellationToken token)
    {
        await projectionService.RebuildAsync(participant.UserId, token);
        var firstDate = Max(contest.StartsOn, participant.EligibleFrom ?? DateOnly.FromDateTime(participant.JoinedAt.UtcDateTime));
        var lastDate = Min(through, contest.EndsOn.AddDays(-1));
        if (lastDate < firstDate) return;
        var goals = await dbContext.HydrationGoals.AsNoTracking().Where(item => item.UserId == participant.UserId && item.EffectiveFrom <= lastDate).OrderBy(item => item.EffectiveFrom).ToArrayAsync(token);
        if (goals.Length == 0) throw new HydrationProfileRequiredException();
        var dailyHydration = await dbContext.DailyHydrations.AsNoTracking().Where(item => item.UserId == participant.UserId && item.LocalDate >= firstDate && item.LocalDate <= lastDate).ToDictionaryAsync(item => item.LocalDate, token);
        var existing = await dbContext.ContestDailyScores.Where(item => item.ContestId == contest.Id && item.UserId == participant.UserId).ToDictionaryAsync(item => item.LocalDate, token);
        var now = timeProvider.GetUtcNow();

        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        {
            var goal = goals.LastOrDefault(item => item.EffectiveFrom <= date);
            if (goal is null) continue;
            var hydrationMl = dailyHydration.TryGetValue(date, out var daily) ? daily.HydrationMl : 0;
            if (existing.TryGetValue(date, out var score)) score.Update(goal.DailyTargetMl, hydrationMl, contest.DailyScoreCap, now);
            else
            {
                score = new ContestDailyScore(contest.Id, participant.UserId, date, goal.DailyTargetMl, hydrationMl, contest.ScoringRuleVersion, contest.DailyScoreCap, now);
                dbContext.ContestDailyScores.Add(score);
            }
            if (date <= finalizeThrough) score.FinalizeAt(now);
        }
        await dbContext.SaveChangesAsync(token);
    }

    private async Task<ContestScoreResponse> MapAsync(Contest contest, string userId, CancellationToken token)
    {
        var scores = await dbContext.ContestDailyScores.AsNoTracking().Where(item => item.ContestId == contest.Id && item.UserId == userId).OrderBy(item => item.LocalDate).ToArrayAsync(token);
        return new ContestScoreResponse(scores.Sum(item => item.Score), scores.Length * contest.DailyScoreCap,
            scores.Select(item => new ContestDailyScoreResponse(item.LocalDate, item.DailyTargetMl, item.HydrationMl, item.Score, item.IsFinal)).ToArray());
    }

    private async Task<DateOnly> LocalTodayAsync(string userId, CancellationToken token)
    {
        var timeZoneId = await dbContext.Profiles.AsNoTracking().Where(item => item.UserId == userId).Select(item => item.TimeZone).SingleOrDefaultAsync(token)
            ?? throw new HydrationProfileRequiredException();
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), ResolveTimeZone(timeZoneId)).Date);
    }

    private static DateOnly Min(DateOnly left, DateOnly right) => left < right ? left : right;
    private static DateOnly Max(DateOnly left, DateOnly right) => left > right ? left : right;
    private static TimeZoneInfo ResolveTimeZone(string value)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(value); }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new ArgumentException("Time zone is invalid.", nameof(value), exception); }
    }
}
