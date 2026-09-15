using System.ComponentModel.DataAnnotations;

namespace Water.Application.Social;

public sealed record SocialProfileResponse(string UserId, string Username, string DisplayName, string? Bio, string Relationship);
public sealed record FriendRequestResponse(Guid Id, SocialProfileResponse Profile, string Direction, DateTimeOffset CreatedAt);
public sealed record CreateFriendRequest([property: Required, MaxLength(20)] string Username);

public interface IFriendService
{
    Task<IReadOnlyCollection<SocialProfileResponse>> SearchAsync(string userId, string query, CancellationToken token);
    Task<IReadOnlyCollection<SocialProfileResponse>> GetFriendsAsync(string userId, CancellationToken token);
    Task<IReadOnlyCollection<FriendRequestResponse>> GetRequestsAsync(string userId, CancellationToken token);
    Task<FriendRequestResponse> RequestAsync(string userId, string username, CancellationToken token);
    Task AcceptAsync(string userId, Guid requestId, CancellationToken token);
    Task RemoveRequestAsync(string userId, Guid requestId, CancellationToken token);
    Task RemoveFriendAsync(string userId, string otherUserId, CancellationToken token);
}

public sealed class SocialConflictException : Exception;
public sealed class SocialNotFoundException : Exception;
