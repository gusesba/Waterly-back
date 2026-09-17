namespace Water.Domain.Competition;

public sealed class ContestRewardCheckpoint
{
    private ContestRewardCheckpoint() { }

    public ContestRewardCheckpoint(Guid contestId, int ruleVersion, DateTimeOffset processedAt)
    {
        Id = Guid.NewGuid();
        ContestId = contestId;
        RuleVersion = ruleVersion;
        ProcessedAt = processedAt;
    }

    public Guid Id { get; private set; }
    public Guid ContestId { get; private set; }
    public int RuleVersion { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }
}
