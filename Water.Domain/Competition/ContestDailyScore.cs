namespace Water.Domain.Competition;

public sealed class ContestDailyScore
{
    private ContestDailyScore() { }

    public ContestDailyScore(Guid contestId, string userId, DateOnly localDate, int dailyTargetMl, int hydrationMl, int ruleVersion, int dailyCap, DateTimeOffset calculatedAt)
    {
        Id = Guid.NewGuid();
        ContestId = contestId;
        UserId = userId;
        LocalDate = localDate;
        RuleVersion = ruleVersion;
        Update(dailyTargetMl, hydrationMl, dailyCap, calculatedAt);
    }

    public Guid Id { get; private set; }
    public Guid ContestId { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public DateOnly LocalDate { get; private set; }
    public int DailyTargetMl { get; private set; }
    public int HydrationMl { get; private set; }
    public decimal Score { get; private set; }
    public int RuleVersion { get; private set; }
    public bool IsFinal { get; private set; }
    public DateTimeOffset CalculatedAt { get; private set; }
    public DateTimeOffset? FinalizedAt { get; private set; }

    public void Update(int dailyTargetMl, int hydrationMl, int dailyCap, DateTimeOffset calculatedAt)
    {
        if (IsFinal) return;
        DailyTargetMl = dailyTargetMl;
        HydrationMl = hydrationMl;
        Score = Calculate(dailyTargetMl, hydrationMl, dailyCap);
        CalculatedAt = calculatedAt;
    }

    public void FinalizeAt(DateTimeOffset finalizedAt)
    {
        if (IsFinal) return;
        IsFinal = true;
        FinalizedAt = finalizedAt;
    }

    public static decimal Calculate(int dailyTargetMl, int hydrationMl, int dailyCap)
    {
        if (dailyTargetMl <= 0) throw new ArgumentOutOfRangeException(nameof(dailyTargetMl));
        return Math.Round(Math.Min((decimal)hydrationMl / dailyTargetMl, 1m) * dailyCap, 2, MidpointRounding.AwayFromZero);
    }
}
