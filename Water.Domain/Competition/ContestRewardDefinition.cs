namespace Water.Domain.Competition;

public sealed class ContestRewardDefinition
{
    private ContestRewardDefinition() { }

    public int Id { get; private set; }
    public int RuleVersion { get; private set; }
    public int DurationDays { get; private set; }
    public int Position { get; private set; }
    public int DropsReward { get; private set; }
    public int PrestigeReward { get; private set; }
}
