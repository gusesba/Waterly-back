using Water.Domain.Hydration;

namespace Water.Tests.Unit;

public sealed class DrinkEntryTests
{
    [Fact]
    public void Constructor_preserves_idempotency_and_source_data()
    {
        var clientEntryId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;

        var entry = new DrinkEntry(
            "user-1",
            clientEntryId,
            350,
            occurredAt,
            "America/Sao_Paulo");

        Assert.Equal(clientEntryId, entry.ClientEntryId);
        Assert.Equal(350, entry.VolumeMl);
        Assert.Equal(occurredAt.UtcDateTime, entry.OccurredAtUtc);
        Assert.Equal("manual", entry.Source);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2001)]
    public void Constructor_rejects_invalid_volume(int volumeMl)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DrinkEntry(
            "user-1",
            Guid.NewGuid(),
            volumeMl,
            DateTimeOffset.UtcNow,
            "UTC"));
    }
}
