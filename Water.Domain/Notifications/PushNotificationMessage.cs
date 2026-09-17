namespace Water.Domain.Notifications;

public sealed class PushNotificationMessage
{
    private PushNotificationMessage() { }

    public PushNotificationMessage(Guid deviceInstallationId, string eventType, string referenceId, string title, string body, string dataJson, string idempotencyKey, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        DeviceInstallationId = deviceInstallationId;
        EventType = eventType;
        ReferenceId = referenceId;
        Title = title;
        Body = body;
        DataJson = dataJson;
        IdempotencyKey = idempotencyKey;
        CreatedAt = createdAt;
        NextAttemptAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid DeviceInstallationId { get; private set; }
    public DeviceInstallation DeviceInstallation { get; private set; } = null!;
    public string EventType { get; private set; } = string.Empty;
    public string ReferenceId { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string DataJson { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public int AttemptCount { get; private set; }
    public string? TicketId { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? LastError { get; private set; }

    public void Accept(string ticketId, DateTimeOffset acceptedAt)
    {
        AttemptCount++;
        TicketId = ticketId;
        AcceptedAt = acceptedAt;
        LastError = null;
    }

    public void Retry(string error, DateTimeOffset nextAttemptAt)
    {
        AttemptCount++;
        TicketId = null;
        AcceptedAt = null;
        LastError = error;
        NextAttemptAt = nextAttemptAt;
    }

    public void Complete(DateTimeOffset completedAt, string? error = null)
    {
        CompletedAt = completedAt;
        LastError = error;
    }
}
