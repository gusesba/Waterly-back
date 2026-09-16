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
        group.MapPut("/{eventId:guid}/reactions/me", SetReactionAsync);
        group.MapDelete("/{eventId:guid}/reactions/me", RemoveReactionAsync);
        return endpoints;
    }

    private static async Task<IResult> SetReactionAsync(Guid eventId, SetFeedReactionRequest request, ClaimsPrincipal principal, IFeedService service, CancellationToken token)
    {
        try { return TypedResults.Ok(await service.SetReactionAsync(UserId(principal), eventId, request.Type, token)); }
        catch (InvalidFeedReactionException) { return Problem(StatusCodes.Status400BadRequest, "Invalid feed reaction.", "invalid_feed_reaction"); }
        catch (OwnFeedEventReactionException) { return Problem(StatusCodes.Status400BadRequest, "Users cannot react to their own feed events.", "own_feed_event_reaction"); }
        catch (FeedEventNotFoundException) { return Problem(StatusCodes.Status404NotFound, "Feed event not found.", "feed_event_not_found"); }
    }

    private static async Task<IResult> RemoveReactionAsync(Guid eventId, ClaimsPrincipal principal, IFeedService service, CancellationToken token)
    {
        try { return TypedResults.Ok(await service.RemoveReactionAsync(UserId(principal), eventId, token)); }
        catch (OwnFeedEventReactionException) { return Problem(StatusCodes.Status400BadRequest, "Users cannot react to their own feed events.", "own_feed_event_reaction"); }
        catch (FeedEventNotFoundException) { return Problem(StatusCodes.Status404NotFound, "Feed event not found.", "feed_event_not_found"); }
    }

    private static IResult Problem(int status, string title, string code) =>
        TypedResults.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["code"] = code });

    private static async Task<IResult> GetAsync(string? cursor, int? limit, ClaimsPrincipal principal, IFeedService service, CancellationToken token)
    {
        try { return TypedResults.Ok(await service.GetAsync(UserId(principal), cursor, limit ?? 20, token)); }
        catch (InvalidFeedCursorException) { return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid feed cursor.", extensions: new Dictionary<string, object?> { ["code"] = "invalid_feed_cursor" }); }
    }

    private static string UserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user has no identifier.");
}
