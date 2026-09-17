namespace Water.Domain.Competition;

public sealed class ContestFinalization
{
    private ContestFinalization() { }

    public ContestFinalization(Guid contestId, int ruleVersion, int participantCount, DateTimeOffset finalizedAt)
    {
        Id = Guid.NewGuid();
        ContestId = contestId;
        RuleVersion = ruleVersion;
        ParticipantCount = participantCount;
        FinalizedAt = finalizedAt;
    }

    public Guid Id { get; private set; }
    public Guid ContestId { get; private set; }
    public int RuleVersion { get; private set; }
    public int ParticipantCount { get; private set; }
    public DateTimeOffset FinalizedAt { get; private set; }
}
