namespace Water.Domain.Competition;

public sealed class Contest
{
    private Contest() { }

    public Contest(string name, DateOnly startsOn, int durationDays, DateTimeOffset createdAt)
    {
        if (durationDays is not (7 or 30)) throw new ArgumentOutOfRangeException(nameof(durationDays));
        Id = Guid.NewGuid();
        Name = name.Trim();
        StartsOn = startsOn;
        EndsOn = startsOn.AddDays(durationDays);
        DurationDays = durationDays;
        ScoringRuleVersion = 1;
        RewardRuleVersion = 1;
        DailyScoreCap = 100;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateOnly StartsOn { get; private set; }
    public DateOnly EndsOn { get; private set; }
    public int DurationDays { get; private set; }
    public int ScoringRuleVersion { get; private set; }
    public int RewardRuleVersion { get; private set; }
    public int DailyScoreCap { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public string Status(DateOnly today) => today < StartsOn ? "upcoming" : today >= EndsOn ? "ended" : "active";
}
