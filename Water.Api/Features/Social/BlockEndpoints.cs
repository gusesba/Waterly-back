using System.Security.Claims;
using Water.Application.Social;

namespace Water.Api.Features.Social;

public static class BlockEndpoints
{
    public static IEndpointRouteBuilder MapBlockEndpoints(this IEndpointRouteBuilder endpoints, bool requireRateLimiting = true)
    {
        var group = endpoints.MapGroup("/api/v1/blocks").RequireAuthorization().WithTags("Social");
        if (requireRateLimiting) group.RequireRateLimiting("social");
        group.MapGet("/", GetAllAsync);
        group.MapPut("/{blockedUserId}", BlockAsync);
        group.MapDelete("/{blockedUserId}", UnblockAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAllAsync(ClaimsPrincipal principal, IBlockService service, CancellationToken token) =>
        TypedResults.Ok(await service.GetAllAsync(UserId(principal), token));

    private static async Task<IResult> BlockAsync(string blockedUserId, ClaimsPrincipal principal, IBlockService service, CancellationToken token)
    {
        try { await service.BlockAsync(UserId(principal), blockedUserId, token); return TypedResults.NoContent(); }
        catch (BlockConflictException) { return TypedResults.Conflict(); }
        catch (BlockNotFoundException) { return TypedResults.NotFound(); }
    }

    private static async Task<IResult> UnblockAsync(string blockedUserId, ClaimsPrincipal principal, IBlockService service, CancellationToken token)
    {
        await service.UnblockAsync(UserId(principal), blockedUserId, token);
        return TypedResults.NoContent();
    }

    private static string UserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated user has no identifier.");
}
