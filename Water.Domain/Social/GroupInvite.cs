namespace Water.Domain.Social;

public sealed class GroupInvite
{
    private GroupInvite() { }

    public GroupInvite(Guid groupId, string createdByUserId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        Id = Guid.NewGuid();
        GroupId = groupId;
        CreatedByUserId = createdByUserId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    public string CreatedByUserId { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsAvailable(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
