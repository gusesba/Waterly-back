namespace Water.Domain.Habits;

public sealed class DailyHydration
{
    private DailyHydration()
    {
    }

    public DailyHydration(
        string userId,
        DateOnly localDate,
        string timeZone,
        int dailyTargetMl,
        int hydrationMl,
        DateTimeOffset? completedAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        LocalDate = localDate;
        Update(timeZone, dailyTargetMl, hydrationMl, completedAt);
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public DateOnly LocalDate { get; private set; }
    public string TimeZone { get; private set; } = "UTC";
    public int DailyTargetMl { get; private set; }
    public int HydrationMl { get; private set; }
    public decimal Progress { get; private set; }
    public bool IsCompleted { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public int RuleVersion { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(
        string timeZone,
        int dailyTargetMl,
        int hydrationMl,
        DateTimeOffset? completedAt)
    {
        TimeZone = timeZone;
        DailyTargetMl = dailyTargetMl;
        HydrationMl = hydrationMl;
        Progress = Math.Min((decimal)hydrationMl / dailyTargetMl, 1m);
        IsCompleted = hydrationMl >= dailyTargetMl;
        CompletedAt = IsCompleted ? completedAt : null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
