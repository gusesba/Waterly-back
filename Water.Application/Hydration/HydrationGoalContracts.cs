using System.ComponentModel.DataAnnotations;

namespace Water.Application.Hydration;
public sealed record UpdateHydrationGoalRequest([property: Range(500, 6000)] int DailyTargetMl);
public sealed record HydrationGoalSettingsResponse(int DailyTargetMl, int? ScheduledTargetMl, DateOnly? EffectiveFrom);
public interface IHydrationGoalService
{
    Task<HydrationGoalSettingsResponse> GetAsync(string userId, CancellationToken token);
    Task<HydrationGoalSettingsResponse> UpdateAsync(string userId, UpdateHydrationGoalRequest request, CancellationToken token);
}
