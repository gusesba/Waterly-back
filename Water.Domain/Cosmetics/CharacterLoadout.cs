namespace Water.Domain.Cosmetics;

public sealed class CharacterLoadout
{
    private CharacterLoadout() { }

    public CharacterLoadout(string userId, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        AuraCode = "natural";
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string AuraCode { get; private set; } = "natural";
    public DateTimeOffset UpdatedAt { get; private set; }

    public void EquipAura(string auraCode, DateTimeOffset updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(auraCode);
        AuraCode = auraCode;
        UpdatedAt = updatedAt;
    }
}
