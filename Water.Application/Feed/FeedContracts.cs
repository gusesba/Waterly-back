namespace Water.Application.Feed;

public sealed record FeedActorResponse(string UserId, string? Username, string? DisplayName, bool IsCurrentUser);
public sealed record FeedEventResponse(Guid Id, string Type, FeedActorResponse Actor, string ReferenceId, string Subject, Guid? GroupId, DateTimeOffset CreatedAt);
public sealed record FeedPageResponse(IReadOnlyCollection<FeedEventResponse> Items, string? NextCursor);

public interface IFeedService
{
    Task<FeedPageResponse> GetAsync(string userId, string? cursor, int limit, CancellationToken token);
}

public sealed class InvalidFeedCursorException : Exception;
