using System.Security.Claims;
using Water.Application.Habits;
using Water.Application.Hydration;

namespace Water.Api.Features.Habits;

public static class HabitEndpoints
{
    public static IEndpointRouteBuilder MapHabitEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/habits/streak", GetStreakAsync)
            .RequireAuthorization()
            .WithTags("Habits");
        return endpoints;
    }

    private static async Task<IResult> GetStreakAsync(
        ClaimsPrincipal principal,
        IHabitService habitService,
        CancellationToken cancellationToken)
    {
        try
        {
            return TypedResults.Ok(await habitService.GetStreakAsync(
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
