namespace Water.Domain.Competition;

public sealed class MedalDefinition
{
    private MedalDefinition() { }

    public MedalDefinition(Guid contestId, string code, string name, DateOnly startsOn, DateOnly endsOn, int ruleVersion, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        ContestId = contestId;
        Code = code;
        Name = name;
        StartsOn = startsOn;
        EndsOn = endsOn;
        RuleVersion = ruleVersion;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid ContestId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public DateOnly StartsOn { get; private set; }
    public DateOnly EndsOn { get; private set; }
    public int RuleVersion { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
