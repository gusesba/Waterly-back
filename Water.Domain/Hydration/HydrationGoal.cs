namespace Water.Domain.Hydration;

public sealed class HydrationGoal
{
    private HydrationGoal()
    {
    }

    public HydrationGoal(string userId, int dailyTargetMl, DateOnly effectiveFrom)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        DailyTargetMl = dailyTargetMl;
        EffectiveFrom = effectiveFrom;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public int DailyTargetMl { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
