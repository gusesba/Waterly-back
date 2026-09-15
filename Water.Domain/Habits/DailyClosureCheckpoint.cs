namespace Water.Domain.Habits;

public sealed class DailyClosureCheckpoint
{
    private DailyClosureCheckpoint() { }

    public DailyClosureCheckpoint(string userId, DateOnly closedThrough, DateTimeOffset updatedAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        ClosedThrough = closedThrough;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public DateOnly ClosedThrough { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void AdvanceTo(DateOnly closedThrough, DateTimeOffset updatedAt)
    {
        if (closedThrough < ClosedThrough)
        {
            throw new ArgumentOutOfRangeException(nameof(closedThrough));
        }

        ClosedThrough = closedThrough;
        UpdatedAt = updatedAt;
    }
}
