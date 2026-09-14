using System.Security.Claims;
using Water.Application.Hydration;

namespace Water.Api.Features.Hydration;

public static class HydrationEndpoints
{
    public static IEndpointRouteBuilder MapHydrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/hydration")
            .RequireAuthorization()
            .WithTags("Hydration");

        group.MapGet("/today", GetTodayAsync);
        group.MapPost("/entries", AddEntryAsync);

        return endpoints;
    }

    private static async Task<IResult> GetTodayAsync(
        ClaimsPrincipal principal,
        IHydrationService hydrationService,
        CancellationToken cancellationToken)
    {
        try
        {
            return TypedResults.Ok(await hydrationService.GetTodayAsync(
                GetUserId(principal),
                cancellationToken));
        }
        catch (HydrationProfileRequiredException)
        {
            return TypedResults.Conflict();
        }
    }

    private static async Task<IResult> AddEntryAsync(
        AddDrinkEntryRequest request,
        ClaimsPrincipal principal,
        IHydrationService hydrationService,
        CancellationToken cancellationToken)
    {
        try
        {
            return TypedResults.Ok(await hydrationService.AddEntryAsync(
                GetUserId(principal),
                request,
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [exception.ParamName ?? "request"] = [exception.Message]
            });
        }
        catch (HydrationProfileRequiredException)
        {
            return TypedResults.Conflict();
        }
        catch (IdempotencyConflictException)
        {
            return TypedResults.Conflict();
        }
    }

    private static string GetUserId(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user has no identifier.");
    }
}
