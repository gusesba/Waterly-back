using Microsoft.EntityFrameworkCore;
using Water.Application.Hydration;
using Water.Domain.Hydration;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Hydration;
public sealed class HydrationGoalService(WaterDbContext dbContext, TimeProvider timeProvider) : IHydrationGoalService
{
    public async Task<HydrationGoalSettingsResponse> GetAsync(string userId, CancellationToken token)
    {
        var today = await TodayAsync(userId, token);
        var current = await dbContext.HydrationGoals.AsNoTracking().Where(item => item.UserId == userId && item.EffectiveFrom <= today)
            .OrderByDescending(item => item.EffectiveFrom).FirstOrDefaultAsync(token) ?? throw new HydrationProfileRequiredException();
        var next = await dbContext.HydrationGoals.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId && item.EffectiveFrom == today.AddDays(1), token);
        return new HydrationGoalSettingsResponse(current.DailyTargetMl, next?.DailyTargetMl, next?.EffectiveFrom);
    }

    public async Task<HydrationGoalSettingsResponse> UpdateAsync(string userId, UpdateHydrationGoalRequest request, CancellationToken token)
    {
        if (request.DailyTargetMl is < 500 or > 6000) throw new ArgumentOutOfRangeException(nameof(request.DailyTargetMl));
        var effectiveFrom = (await TodayAsync(userId, token)).AddDays(1);
        var scheduled = await dbContext.HydrationGoals.SingleOrDefaultAsync(item => item.UserId == userId && item.EffectiveFrom == effectiveFrom, token);
        if (scheduled == null) dbContext.HydrationGoals.Add(new HydrationGoal(userId, request.DailyTargetMl, effectiveFrom));
        else scheduled.UpdateTarget(request.DailyTargetMl);
        await dbContext.SaveChangesAsync(token);
        return await GetAsync(userId, token);
    }

    private async Task<DateOnly> TodayAsync(string userId, CancellationToken token)
    {
        var zone = await dbContext.Profiles.AsNoTracking().Where(item => item.UserId == userId).Select(item => item.TimeZone).SingleOrDefaultAsync(token)
            ?? throw new HydrationProfileRequiredException();
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(zone)).Date);
    }
}
