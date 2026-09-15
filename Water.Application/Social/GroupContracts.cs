using System.ComponentModel.DataAnnotations;

namespace Water.Application.Social;

public sealed record GroupSummaryResponse(Guid Id, string Name, string? Description, bool IsOwner, int MemberCount, DateTimeOffset UpdatedAt);
public sealed record GroupMemberResponse(string UserId, string Username, string DisplayName, string Role, DateTimeOffset JoinedAt);
public sealed record GroupDetailResponse(Guid Id, string Name, string? Description, bool IsOwner, IReadOnlyCollection<GroupMemberResponse> Members, DateTimeOffset UpdatedAt);
public sealed record GroupCapacityResponse(int Used, int Limit);
public sealed record GroupInviteResponse(string Token, DateTimeOffset ExpiresAt);
public sealed record GroupInvitePreviewResponse(Guid GroupId, string GroupName, string OwnerDisplayName, int MemberCount, DateTimeOffset ExpiresAt, bool IsMember);
public sealed record SaveGroupRequest([property: Required, MinLength(3), MaxLength(40)] string Name, [property: MaxLength(160)] string? Description);
public sealed record AddGroupMemberRequest([property: Required] string UserId);

public interface IGroupService
{
    Task<IReadOnlyCollection<GroupSummaryResponse>> GetAllAsync(string userId, CancellationToken token);
    Task<GroupDetailResponse> GetAsync(string userId, Guid groupId, CancellationToken token);
    Task<GroupDetailResponse> CreateAsync(string userId, SaveGroupRequest request, CancellationToken token);
    Task<GroupDetailResponse> UpdateAsync(string userId, Guid groupId, SaveGroupRequest request, CancellationToken token);
    Task DeleteAsync(string userId, Guid groupId, CancellationToken token);
    Task<GroupDetailResponse> AddMemberAsync(string userId, Guid groupId, string memberId, CancellationToken token);
    Task RemoveMemberAsync(string userId, Guid groupId, string memberId, CancellationToken token);
    Task LeaveAsync(string userId, Guid groupId, CancellationToken token);
    Task<GroupCapacityResponse> GetCapacityAsync(string userId, CancellationToken token);
    Task<GroupInviteResponse> CreateInviteAsync(string userId, Guid groupId, CancellationToken token);
    Task RevokeInvitesAsync(string userId, Guid groupId, CancellationToken token);
    Task<GroupInvitePreviewResponse> GetInviteAsync(string? userId, string inviteToken, CancellationToken token);
    Task<GroupDetailResponse> AcceptInviteAsync(string userId, string inviteToken, CancellationToken token);
}

public sealed class GroupNotFoundException : Exception;
public sealed class GroupConflictException : Exception;
public sealed class GroupCapacityException : Exception;
public sealed class GroupInviteNotFoundException : Exception;
