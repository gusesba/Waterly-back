namespace Water.Domain.Progression;

public sealed class PrestigeLedgerEntry
{
    private PrestigeLedgerEntry()
    {
    }

    public PrestigeLedgerEntry(
        string userId,
        int amount,
        string entryType,
        string referenceType,
        string referenceId,
        string idempotencyKey,
        int ruleVersion,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Amount = amount;
        EntryType = entryType;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        IdempotencyKey = idempotencyKey;
        RuleVersion = ruleVersion;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public int Amount { get; private set; }
    public string EntryType { get; private set; } = string.Empty;
    public string ReferenceType { get; private set; } = string.Empty;
    public string ReferenceId { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public int RuleVersion { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
