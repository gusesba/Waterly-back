namespace Water.Domain.Social;

public sealed class PrivateGroup
{
    private PrivateGroup() { }

    public PrivateGroup(string ownerId, string name, string? description, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        OwnerId = ownerId;
        CreatedAt = createdAt;
        Update(name, description, createdAt);
    }

    public Guid Id { get; private set; }
    public string OwnerId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void TransferOwnership(string ownerId, DateTimeOffset updatedAt)
    {
        OwnerId = ownerId;
        UpdatedAt = updatedAt;
    }

    public void Update(string name, string? description, DateTimeOffset updatedAt)
    {
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UpdatedAt = updatedAt;
    }
}
