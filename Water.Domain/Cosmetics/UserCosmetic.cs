namespace Water.Domain.Cosmetics;

public sealed class UserCosmetic
{
    private UserCosmetic() { }

    public UserCosmetic(string userId, string cosmeticCode, DateTimeOffset unlockedAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        CosmeticCode = cosmeticCode;
        UnlockedAt = unlockedAt;
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string CosmeticCode { get; private set; } = string.Empty;
    public DateTimeOffset UnlockedAt { get; private set; }
}
