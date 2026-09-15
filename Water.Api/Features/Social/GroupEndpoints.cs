using System.Security.Claims;
using Water.Application.Social;

namespace Water.Api.Features.Social;

public static class GroupEndpoints
{
    public static IEndpointRouteBuilder MapGroupEndpoints(this IEndpointRouteBuilder endpoints, bool requireRateLimiting = true)
    {
        var group = endpoints.MapGroup("/api/v1/groups").RequireAuthorization().WithTags("Groups");
        if (requireRateLimiting) group.RequireRateLimiting("social");
        group.MapGet("/", GetAllAsync);
        group.MapGet("/capacity", GetCapacityAsync);
        group.MapPost("/", CreateAsync);
        group.MapGet("/{groupId:guid}", GetAsync);
        group.MapPut("/{groupId:guid}", UpdateAsync);
        group.MapDelete("/{groupId:guid}", DeleteAsync);
        group.MapPost("/{groupId:guid}/members", AddMemberAsync);
        group.MapDelete("/{groupId:guid}/members/{memberId}", RemoveMemberAsync);
        group.MapDelete("/{groupId:guid}/membership", LeaveAsync);
        group.MapPost("/{groupId:guid}/invite", CreateInviteAsync);
        group.MapDelete("/{groupId:guid}/invite", RevokeInvitesAsync);

        var invites = endpoints.MapGroup("/api/v1/invites/group").WithTags("Group invites");
        if (requireRateLimiting) invites.RequireRateLimiting("social");
        invites.MapGet("/{inviteToken}", GetInviteAsync).AllowAnonymous();
        invites.MapPost("/{inviteToken}/accept", AcceptInviteAsync).RequireAuthorization();
        return endpoints;
    }

    private static Task<IReadOnlyCollection<GroupSummaryResponse>> GetAllAsync(ClaimsPrincipal principal, IGroupService service, CancellationToken token) => service.GetAllAsync(UserId(principal), token);
    private static async Task<IResult> GetAsync(Guid groupId, ClaimsPrincipal principal, IGroupService service, CancellationToken token) => await Result(() => service.GetAsync(UserId(principal), groupId, token));
    private static Task<GroupCapacityResponse> GetCapacityAsync(ClaimsPrincipal principal, IGroupService service, CancellationToken token) => service.GetCapacityAsync(UserId(principal), token);
    private static async Task<IResult> CreateAsync(SaveGroupRequest request, ClaimsPrincipal principal, IGroupService service, CancellationToken token) => await Result(() => service.CreateAsync(UserId(principal), request, token));
    private static async Task<IResult> UpdateAsync(Guid groupId, SaveGroupRequest request, ClaimsPrincipal principal, IGroupService service, CancellationToken token) => await Result(() => service.UpdateAsync(UserId(principal), groupId, request, token));
    private static async Task<IResult> DeleteAsync(Guid groupId, ClaimsPrincipal principal, IGroupService service, CancellationToken token) => await EmptyResult(() => service.DeleteAsync(UserId(principal), groupId, token));
    private static async Task<IResult> AddMemberAsync(Guid groupId, AddGroupMemberRequest request, ClaimsPrincipal principal, IGroupService service, CancellationToken token) => await Result(() => service.AddMemberAsync(UserId(principal), groupId, request.UserId, token));
    private static async Task<IResult> RemoveMemberAsync(Guid groupId, string memberId, ClaimsPrincipal principal, IGroupService service, CancellationToken token) => await EmptyResult(() => service.RemoveMemberAsync(UserId(principal), groupId, memberId, token));
    private static async Task<IResult> LeaveAsync(Guid groupId, ClaimsPrincipal principal, IGroupService service, CancellationToken token) => await EmptyResult(() => service.LeaveAsync(UserId(principal), groupId, token));
    private static async Task<IResult> CreateInviteAsync(Guid groupId, ClaimsPrincipal principal, IGroupService service, CancellationToken token)
    {
        try { return TypedResults.Ok(await service.CreateInviteAsync(UserId(principal), groupId, token)); }
        catch (GroupNotFoundException) { return TypedResults.NotFound(); }
    }
    private static async Task<IResult> RevokeInvitesAsync(Guid groupId, ClaimsPrincipal principal, IGroupService service, CancellationToken token) => await EmptyResult(() => service.RevokeInvitesAsync(UserId(principal), groupId, token));
    private static async Task<IResult> GetInviteAsync(string inviteToken, ClaimsPrincipal principal, IGroupService service, CancellationToken token)
    {
        try { return TypedResults.Ok(await service.GetInviteAsync(principal.FindFirstValue(ClaimTypes.NameIdentifier), inviteToken, token)); }
        catch (GroupInviteNotFoundException) { return InviteUnavailable(); }
    }
    private static async Task<IResult> AcceptInviteAsync(string inviteToken, ClaimsPrincipal principal, IGroupService service, CancellationToken token)
    {
        try { return TypedResults.Ok(await service.AcceptInviteAsync(UserId(principal), inviteToken, token)); }
        catch (GroupInviteNotFoundException) { return InviteUnavailable(); }
        catch (GroupCapacityException) { return CapacityReached(); }
    }

    private static async Task<IResult> Result(Func<Task<GroupDetailResponse>> action)
    {
        try { return TypedResults.Ok(await action()); }
        catch (GroupNotFoundException) { return TypedResults.NotFound(); }
        catch (GroupConflictException) { return TypedResults.Conflict(); }
        catch (GroupCapacityException) { return CapacityReached(); }
    }
    private static async Task<IResult> EmptyResult(Func<Task> action)
    {
        try { await action(); return TypedResults.NoContent(); }
        catch (GroupNotFoundException) { return TypedResults.NotFound(); }
        catch (GroupConflictException) { return TypedResults.Conflict(); }
        catch (GroupCapacityException) { return CapacityReached(); }
    }
    private static IResult CapacityReached() => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Group capacity reached.", extensions: new Dictionary<string, object?> { ["code"] = "group_capacity_reached" });
    private static IResult InviteUnavailable() => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Group invite unavailable.", extensions: new Dictionary<string, object?> { ["code"] = "group_invite_unavailable" });
    private static string UserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user has no identifier.");
}
