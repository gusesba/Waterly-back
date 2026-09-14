namespace Water.Domain.Hydration;

public sealed class DrinkEntry
{
    private DrinkEntry()
    {
    }

    public DrinkEntry(
        string userId,
        Guid clientEntryId,
        int volumeMl,
        DateTimeOffset occurredAt,
        string timeZone,
        string beverageCode = "water",
        int? hydrationMl = null)
    {
        if (clientEntryId == Guid.Empty)
        {
            throw new ArgumentException("Client entry identifier is required.", nameof(clientEntryId));
        }

        if (volumeMl is < 1 or > 2000)
        {
            throw new ArgumentOutOfRangeException(nameof(volumeMl));
        }

        Id = Guid.NewGuid();
        UserId = userId;
        ClientEntryId = clientEntryId;
        VolumeMl = volumeMl;
        BeverageCode = beverageCode;
        HydrationMl = hydrationMl ?? volumeMl;
        OccurredAtUtc = occurredAt.UtcDateTime;
        TimeZone = timeZone;
        Source = "manual";
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public Guid ClientEntryId { get; private set; }
    public int VolumeMl { get; private set; }
    public string BeverageCode { get; private set; } = "water";
    public int HydrationMl { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string TimeZone { get; private set; } = "UTC";
    public string Source { get; private set; } = "manual";
    public DateTimeOffset CreatedAt { get; private set; }

    public void Update(int volumeMl, string beverageCode, int hydrationMl)
    {
        if (volumeMl is < 1 or > 2000)
        {
            throw new ArgumentOutOfRangeException(nameof(volumeMl));
        }

        VolumeMl = volumeMl;
        BeverageCode = beverageCode;
        HydrationMl = hydrationMl;
    }
}
