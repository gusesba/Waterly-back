namespace Water.Infrastructure.Notifications;

public sealed class PushNotificationOptions
{
    public const string SectionName = "PushNotifications";

    public bool Enabled { get; init; }
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(1);
    public int BatchSize { get; init; } = 100;
    public int MaxAttempts { get; init; } = 5;
    public TimeSpan ReceiptDelay { get; init; } = TimeSpan.FromMinutes(15);
    public string? ExpoAccessToken { get; init; }
}
