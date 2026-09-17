namespace Water.Domain.Competition;

public sealed class UserMedal
{
    private UserMedal() { }

    public UserMedal(Guid medalDefinitionId, string userId, int position, int ruleVersion, DateTimeOffset awardedAt)
    {
        Id = Guid.NewGuid();
        MedalDefinitionId = medalDefinitionId;
        UserId = userId;
        Position = position;
        RuleVersion = ruleVersion;
        AwardedAt = awardedAt;
    }

    public Guid Id { get; private set; }
    public Guid MedalDefinitionId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public int Position { get; private set; }
    public int RuleVersion { get; private set; }
    public DateTimeOffset AwardedAt { get; private set; }
}
