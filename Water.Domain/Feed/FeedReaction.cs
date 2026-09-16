namespace Water.Domain.Feed;

public sealed class FeedReaction
{
    private FeedReaction() { }

    public FeedReaction(Guid feedEventId, string userId, string type, DateTimeOffset createdAt)
    {
        FeedEventId = feedEventId;
        UserId = userId;
        Type = type;
        CreatedAt = createdAt;
    }

    public Guid FeedEventId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string Type { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public void ChangeType(string type) => Type = type;
}
