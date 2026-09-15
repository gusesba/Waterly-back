using Water.Domain.Habits;

namespace Water.Tests.Unit;

public sealed class DailyClosureCheckpointTests
{
    [Fact]
    public void Checkpoint_only_moves_forward()
    {
        var updatedAt = DateTimeOffset.UtcNow;
        var checkpoint = new DailyClosureCheckpoint("user-1", new DateOnly(2026, 9, 14), updatedAt);

        checkpoint.AdvanceTo(new DateOnly(2026, 9, 15), updatedAt.AddMinutes(1));

        Assert.Equal(new DateOnly(2026, 9, 15), checkpoint.ClosedThrough);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            checkpoint.AdvanceTo(new DateOnly(2026, 9, 14), updatedAt.AddMinutes(2)));
    }
}
