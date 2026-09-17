using System.ComponentModel.DataAnnotations;

namespace Water.Application.Notifications;

public sealed record UpsertDeviceInstallationRequest(
    [property: Required, MaxLength(256)] string ExpoPushToken,
    [property: Required, MaxLength(16)] string Platform,
    [property: Required, MaxLength(16)] string Locale);
public sealed record DeviceInstallationResponse(Guid Id, string Platform, string Locale, DateTimeOffset UpdatedAt);

public interface IDeviceInstallationService
{
    Task<DeviceInstallationResponse> UpsertAsync(string userId, Guid installationId, UpsertDeviceInstallationRequest request, CancellationToken token);
    Task DisableAsync(string userId, Guid installationId, CancellationToken token);
}

public interface IContestNotificationOutbox
{
    Task QueueResultAsync(string userId, Guid contestId, string contestName, int position, int drops, int prestige, int? medalPosition, int ruleVersion, DateTimeOffset createdAt, CancellationToken token);
}

public sealed class InvalidDeviceInstallationException : Exception;
public sealed class DeviceInstallationConflictException : Exception;
