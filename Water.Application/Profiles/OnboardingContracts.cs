using System.ComponentModel.DataAnnotations;

namespace Water.Application.Profiles;

public sealed record CompleteOnboardingRequest(
    [property: Range(13, 120)] int Age,
    [property: Range(100, 250)] int HeightCm,
    [property: Range(typeof(decimal), "30", "300")] decimal WeightKg,
    [property: Range(500, 6000)] int DailyTargetMl,
    [property: Required, MinLength(1)] string[] Goals,
    [property: Required, StringLength(50)] string TimeZone);

public sealed record CurrentUserResponse(
    string Email,
    bool HasCompletedOnboarding,
    ProfileResponse? Profile,
    HydrationGoalResponse? HydrationGoal);

public sealed record ProfileResponse(
    int Age,
    int HeightCm,
    decimal WeightKg,
    IReadOnlyCollection<string> Goals);

public sealed record HydrationGoalResponse(
    int DailyTargetMl,
    DateOnly EffectiveFrom,
    string TimeZone);
