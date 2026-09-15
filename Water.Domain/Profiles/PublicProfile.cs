namespace Water.Domain.Profiles;

public sealed class PublicProfile
{
    private PublicProfile() { }

    public PublicProfile(string userId, string username, string displayName, string? bio)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        CreatedAt = DateTimeOffset.UtcNow;
        Update(username, displayName, bio);
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public string NormalizedUsername { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? Bio { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string username, string displayName, string? bio)
    {
        Username = username.Trim().ToLowerInvariant();
        NormalizedUsername = Username.ToUpperInvariant();
        DisplayName = displayName.Trim();
        Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
