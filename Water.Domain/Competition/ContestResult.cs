namespace Water.Domain.Competition;

public sealed class ContestResult
{
    private ContestResult() { }

    public ContestResult(
        Guid contestId,
        string userId,
        int position,
        decimal totalScore,
        int scoredDays,
        bool isTied,
        string? username,
        string? displayName,
        DateTimeOffset finalizedAt)
    {
        Id = Guid.NewGuid();
        ContestId = contestId;
        UserId = userId;
        Position = position;
        TotalScore = totalScore;
        ScoredDays = scoredDays;
        IsTied = isTied;
        Username = username;
        DisplayName = displayName;
        FinalizedAt = finalizedAt;
    }

    public Guid Id { get; private set; }
    public Guid ContestId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public int Position { get; private set; }
    public decimal TotalScore { get; private set; }
    public int ScoredDays { get; private set; }
    public bool IsTied { get; private set; }
    public string? Username { get; private set; }
    public string? DisplayName { get; private set; }
    public DateTimeOffset FinalizedAt { get; private set; }
}
