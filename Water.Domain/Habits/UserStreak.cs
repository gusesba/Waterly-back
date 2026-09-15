namespace Water.Domain.Habits;

public sealed class UserStreak
{
    private UserStreak()
    {
    }

    public UserStreak(string userId)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public int Current { get; private set; }
    public int Longest { get; private set; }
    public DateOnly? LastCompletedDate { get; private set; }
    public int RuleVersion { get; private set; } = 1;
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(int current, int longest, DateOnly? lastCompletedDate)
    {
        Current = current;
        Longest = longest;
        LastCompletedDate = lastCompletedDate;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
