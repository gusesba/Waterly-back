namespace Water.Domain.Social;

public sealed class GroupMembership
{
    private GroupMembership() { }

    public GroupMembership(Guid groupId, string userId, string role, DateTimeOffset joinedAt)
    {
        Id = Guid.NewGuid();
        GroupId = groupId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
    }

    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string Role { get; private set; } = "member";
    public DateTimeOffset JoinedAt { get; private set; }
}
