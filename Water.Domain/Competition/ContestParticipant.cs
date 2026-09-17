namespace Water.Domain.Competition;

public sealed class ContestParticipant
{
    private ContestParticipant() { }

    public ContestParticipant(Guid contestId, string userId, Guid clientOperationId, DateTimeOffset joinedAt)
    {
        Id = Guid.NewGuid();
        ContestId = contestId;
        UserId = userId;
        ClientOperationId = clientOperationId;
        JoinedAt = joinedAt;
    }

    public Guid Id { get; private set; }
    public Guid ContestId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public Guid ClientOperationId { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }
}
