using System.Security.Claims;
using Water.Application.Feed;

namespace Water.Api.Features.Feed;

public static class FeedEndpoints
{
    public static IEndpointRouteBuilder MapFeedEndpoints(this IEndpointRouteBuilder endpoints, bool requireRateLimiting = true)
    {
        var group = endpoints.MapGroup("/api/v1/feed").RequireAuthorization().WithTags("Feed");
        if (requireRateLimiting) group.RequireRateLimiting("social");
        group.MapGet("/", GetAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(string? cursor, int? limit, ClaimsPrincipal principal, IFeedService service, CancellationToken token)
    {
        try { return TypedResults.Ok(await service.GetAsync(UserId(principal), cursor, limit ?? 20, token)); }
        catch (InvalidFeedCursorException) { return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid feed cursor.", extensions: new Dictionary<string, object?> { ["code"] = "invalid_feed_cursor" }); }
    }

    private static string UserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user has no identifier.");
}
