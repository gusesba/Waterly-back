using System.Security.Claims;
using Water.Application.Habits;
using Water.Application.Hydration;

namespace Water.Api.Features.Achievements;

public static class AchievementEndpoints
{
    public static IEndpointRouteBuilder MapAchievementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/achievements", GetAchievementsAsync)
            .RequireAuthorization()
            .WithTags("Achievements");
        return endpoints;
    }

    private static async Task<IResult> GetAchievementsAsync(
        ClaimsPrincipal principal,
        IAchievementService achievementService,
        CancellationToken cancellationToken)
    {
        try
        {
            return TypedResults.Ok(await achievementService.GetAchievementsAsync(
                principal.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? throw new InvalidOperationException("Authenticated user has no identifier."),
                cancellationToken));
        }
        catch (HydrationProfileRequiredException)
        {
            return TypedResults.Conflict();
        }
    }
}
