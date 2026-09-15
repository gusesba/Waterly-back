using System.Security.Claims;
using Water.Application.Social;

namespace Water.Api.Features.Social;

public static class FriendEndpoints
{
    public static IEndpointRouteBuilder MapFriendEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1").RequireAuthorization().RequireRateLimiting("social").WithTags("Social");
        group.MapGet("/profiles/search", SearchAsync);
        group.MapGet("/friends", GetFriendsAsync);
        group.MapGet("/friends/requests", GetRequestsAsync);
        group.MapPost("/friends/requests", RequestAsync);
        group.MapPut("/friends/requests/{requestId:guid}/accept", AcceptAsync);
        group.MapDelete("/friends/requests/{requestId:guid}", RemoveRequestAsync);
        group.MapDelete("/friends/{otherUserId}", RemoveFriendAsync);
        return endpoints;
    }

    private static async Task<IResult> SearchAsync(string query, ClaimsPrincipal principal, IFriendService service, CancellationToken token) =>
        TypedResults.Ok(await service.SearchAsync(UserId(principal), query, token));
    private static async Task<IResult> GetFriendsAsync(ClaimsPrincipal principal, IFriendService service, CancellationToken token) =>
        TypedResults.Ok(await service.GetFriendsAsync(UserId(principal), token));
    private static async Task<IResult> GetRequestsAsync(ClaimsPrincipal principal, IFriendService service, CancellationToken token) =>
        TypedResults.Ok(await service.GetRequestsAsync(UserId(principal), token));

    private static async Task<IResult> RequestAsync(CreateFriendRequest request, ClaimsPrincipal principal, IFriendService service, CancellationToken token)
    {
        try { return TypedResults.Ok(await service.RequestAsync(UserId(principal), request.Username, token)); }
        catch (SocialNotFoundException) { return TypedResults.NotFound(); }
        catch (SocialConflictException) { return TypedResults.Conflict(); }
    }

    private static async Task<IResult> AcceptAsync(Guid requestId, ClaimsPrincipal principal, IFriendService service, CancellationToken token)
    {
        try { await service.AcceptAsync(UserId(principal), requestId, token); return TypedResults.NoContent(); }
        catch (SocialNotFoundException) { return TypedResults.NotFound(); }
        catch (SocialConflictException) { return TypedResults.Conflict(); }
    }

    private static async Task<IResult> RemoveRequestAsync(Guid requestId, ClaimsPrincipal principal, IFriendService service, CancellationToken token)
    {
        try { await service.RemoveRequestAsync(UserId(principal), requestId, token); return TypedResults.NoContent(); }
        catch (SocialNotFoundException) { return TypedResults.NotFound(); }
    }

    private static async Task<IResult> RemoveFriendAsync(string otherUserId, ClaimsPrincipal principal, IFriendService service, CancellationToken token)
    {
        try { await service.RemoveFriendAsync(UserId(principal), otherUserId, token); return TypedResults.NoContent(); }
        catch (SocialNotFoundException) { return TypedResults.NotFound(); }
    }

    private static string UserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated user has no identifier.");
}
