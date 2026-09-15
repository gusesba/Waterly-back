namespace Water.Domain.Social;

public sealed class Friendship
{
    private Friendship() { }

    public Friendship(string requesterId, string addresseeId, DateTimeOffset createdAt)
    {
        if (requesterId == addresseeId) throw new ArgumentException("Users must be different.");
        Id = Guid.NewGuid();
        UserLowId = string.CompareOrdinal(requesterId, addresseeId) < 0 ? requesterId : addresseeId;
        UserHighId = requesterId == UserLowId ? addresseeId : requesterId;
        RequestedByUserId = requesterId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string UserLowId { get; private set; } = string.Empty;
    public string UserHighId { get; private set; } = string.Empty;
    public string RequestedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public bool IsAccepted => AcceptedAt is not null;

    public bool Includes(string userId) => UserLowId == userId || UserHighId == userId;
    public string OtherUserId(string userId) => UserLowId == userId ? UserHighId : UserLowId;

    public void Accept(string userId, DateTimeOffset acceptedAt)
    {
        if (IsAccepted || !Includes(userId) || RequestedByUserId == userId)
            throw new InvalidOperationException("Friend request cannot be accepted by this user.");
        AcceptedAt = acceptedAt;
    }
}
