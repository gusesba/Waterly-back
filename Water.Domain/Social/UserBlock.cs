namespace Water.Domain.Social;

public sealed class UserBlock
{
    private UserBlock() { }

    public UserBlock(string blockerUserId, string blockedUserId, DateTimeOffset createdAt)
    {
        if (blockerUserId == blockedUserId) throw new ArgumentException("Users must be different.");
        BlockerUserId = blockerUserId;
        BlockedUserId = blockedUserId;
        CreatedAt = createdAt;
    }

    public string BlockerUserId { get; private set; } = string.Empty;
    public string BlockedUserId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
}
