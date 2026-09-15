using System.Security.Claims;
using Water.Application.Cosmetics;
using Water.Application.Hydration;

namespace Water.Api.Features.Cosmetics;

public static class CosmeticEndpoints
{
    public static IEndpointRouteBuilder MapCosmeticEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/cosmetics", GetAsync).RequireAuthorization().WithTags("Cosmetics");
        endpoints.MapGet("/api/v1/profile/loadout", GetAsync).RequireAuthorization().WithTags("Cosmetics");
        endpoints.MapPut("/api/v1/profile/loadout", UpdateAsync).RequireAuthorization().WithTags("Cosmetics");
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        ClaimsPrincipal principal,
        ICosmeticService cosmeticService,
        CancellationToken cancellationToken)
    {
        try
        {
            return TypedResults.Ok(await cosmeticService.GetAsync(GetUserId(principal), cancellationToken));
        }
        catch (HydrationProfileRequiredException)
        {
            return TypedResults.Conflict();
        }
    }

    private static async Task<IResult> UpdateAsync(
        UpdateCharacterLoadoutRequest request,
        ClaimsPrincipal principal,
        ICosmeticService cosmeticService,
        CancellationToken cancellationToken)
    {
        try
        {
            return TypedResults.Ok(await cosmeticService.UpdateAsync(
                GetUserId(principal), request.AuraCode, cancellationToken));
        }
        catch (CosmeticNotAvailableException)
        {
            return TypedResults.Conflict();
        }
        catch (HydrationProfileRequiredException)
        {
            return TypedResults.Conflict();
        }
    }

    private static string GetUserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated user has no identifier.");
}
