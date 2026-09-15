using System.Security.Claims;
using Water.Application.Progression;

namespace Water.Api.Features.Progression;

public static class ProgressionEndpoints
{
    public static IEndpointRouteBuilder MapProgressionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/wallet", GetDropsAsync)
            .RequireAuthorization()
            .WithTags("Progression");
        endpoints.MapGet("/api/v1/prestige", GetPrestigeAsync)
            .RequireAuthorization()
            .WithTags("Progression");
        return endpoints;
    }

    private static Task<ProgressionBalanceResponse> GetDropsAsync(
        ClaimsPrincipal principal,
        IProgressionService progressionService,
        CancellationToken cancellationToken) =>
        progressionService.GetDropsAsync(GetUserId(principal), cancellationToken);

    private static Task<ProgressionBalanceResponse> GetPrestigeAsync(
        ClaimsPrincipal principal,
        IProgressionService progressionService,
        CancellationToken cancellationToken) =>
        progressionService.GetPrestigeAsync(GetUserId(principal), cancellationToken);

    private static string GetUserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user has no identifier.");
}
