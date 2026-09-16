namespace Water.Domain.Feed;

public sealed class FeedEvent
{
    private FeedEvent() { }

    public FeedEvent(string actorUserId, string eventType, string audience, string referenceId, string subject, string idempotencyKey, DateTimeOffset createdAt, Guid? groupId = null)
    {
        Id = Guid.NewGuid();
        ActorUserId = actorUserId;
        EventType = eventType;
        Audience = audience;
        ReferenceId = referenceId;
        Subject = subject;
        IdempotencyKey = idempotencyKey;
        CreatedAt = createdAt;
        GroupId = groupId;
        SortKey = $"{createdAt.ToUnixTimeMilliseconds():D13}:{Id:N}";
    }

    public Guid Id { get; private set; }
    public string ActorUserId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string Audience { get; private set; } = string.Empty;
    public string ReferenceId { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? GroupId { get; private set; }
    public string SortKey { get; private set; } = string.Empty;
}
