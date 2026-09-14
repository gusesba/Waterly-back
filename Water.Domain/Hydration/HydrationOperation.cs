namespace Water.Domain.Hydration;

public sealed class HydrationOperation
{
    private HydrationOperation()
    {
    }

    public HydrationOperation(
        string userId,
        Guid clientOperationId,
        Guid entryId,
        string operationType,
        string payload)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        ClientOperationId = clientOperationId;
        EntryId = entryId;
        OperationType = operationType;
        Payload = payload;
        ProcessedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public Guid ClientOperationId { get; private set; }
    public Guid EntryId { get; private set; }
    public string OperationType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; private set; }
}
