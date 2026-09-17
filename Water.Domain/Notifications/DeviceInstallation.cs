namespace Water.Domain.Notifications;

public sealed class DeviceInstallation
{
    private DeviceInstallation() { }

    public DeviceInstallation(Guid id, string userId, string expoPushToken, string platform, string locale, DateTimeOffset updatedAt)
    {
        Id = id;
        Update(userId, expoPushToken, platform, locale, updatedAt);
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string ExpoPushToken { get; private set; } = string.Empty;
    public string Platform { get; private set; } = string.Empty;
    public string Locale { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DisabledAt { get; private set; }

    public void Update(string userId, string expoPushToken, string platform, string locale, DateTimeOffset updatedAt)
    {
        UserId = userId;
        ExpoPushToken = expoPushToken;
        Platform = platform;
        Locale = locale;
        UpdatedAt = updatedAt;
        DisabledAt = null;
    }

    public void Disable(DateTimeOffset disabledAt)
    {
        DisabledAt ??= disabledAt;
        UpdatedAt = disabledAt;
    }
}
