namespace Water.Application.Feed;

public sealed record FeedActorResponse(string UserId, string? Username, string? DisplayName, bool IsCurrentUser);
public sealed record FeedReactionSummaryResponse(IReadOnlyDictionary<string, int> Counts, string? CurrentUserReaction);
public sealed record FeedEventResponse(Guid Id, string Type, FeedActorResponse Actor, string ReferenceId, string Subject, Guid? GroupId, DateTimeOffset CreatedAt, FeedReactionSummaryResponse Reactions);
public sealed record FeedPageResponse(IReadOnlyCollection<FeedEventResponse> Items, string? NextCursor);
public sealed record SetFeedReactionRequest(string Type);

public interface IFeedService
{
    Task<FeedPageResponse> GetAsync(string userId, string? cursor, int limit, CancellationToken token);
    Task<FeedReactionSummaryResponse> SetReactionAsync(string userId, Guid eventId, string type, CancellationToken token);
    Task<FeedReactionSummaryResponse> RemoveReactionAsync(string userId, Guid eventId, CancellationToken token);
}

public sealed class InvalidFeedCursorException : Exception;
public sealed class InvalidFeedReactionException : Exception;
public sealed class FeedEventNotFoundException : Exception;
public sealed class OwnFeedEventReactionException : Exception;
