using Microsoft.EntityFrameworkCore;
using Water.Application.Notifications;
using Water.Domain.Notifications;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Notifications;

public sealed class DeviceInstallationService(WaterDbContext dbContext, TimeProvider timeProvider) : IDeviceInstallationService
{
    public async Task<DeviceInstallationResponse> UpsertAsync(string userId, Guid installationId, UpsertDeviceInstallationRequest request, CancellationToken token)
    {
        var pushToken = request.ExpoPushToken.Trim();
        var platform = request.Platform.Trim().ToLowerInvariant();
        var locale = request.Locale.StartsWith("pt", StringComparison.OrdinalIgnoreCase) ? "pt-BR" : "en";
        if (installationId == Guid.Empty || !IsExpoPushToken(pushToken) || platform is not ("android" or "ios"))
            throw new InvalidDeviceInstallationException();

        var installation = await dbContext.DeviceInstallations.SingleOrDefaultAsync(item => item.Id == installationId, token);
        if (installation is not null && installation.UserId != userId) throw new DeviceInstallationConflictException();

        var now = timeProvider.GetUtcNow();
        var previousOwners = await dbContext.DeviceInstallations
            .Where(item => item.ExpoPushToken == pushToken && item.Id != installationId && item.DisabledAt == null)
            .ToArrayAsync(token);
        foreach (var previous in previousOwners) previous.Disable(now);

        if (installation is null)
        {
            installation = new DeviceInstallation(installationId, userId, pushToken, platform, locale, now);
            dbContext.DeviceInstallations.Add(installation);
        }
        else installation.Update(userId, pushToken, platform, locale, now);

        await dbContext.SaveChangesAsync(token);
        return new DeviceInstallationResponse(installation.Id, installation.Platform, installation.Locale, installation.UpdatedAt);
    }

    public async Task DisableAsync(string userId, Guid installationId, CancellationToken token)
    {
        var installation = await dbContext.DeviceInstallations.SingleOrDefaultAsync(item => item.Id == installationId && item.UserId == userId, token);
        if (installation is null) return;
        installation.Disable(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(token);
    }

    private static bool IsExpoPushToken(string value) =>
        ((value.StartsWith("ExpoPushToken[", StringComparison.Ordinal) || value.StartsWith("ExponentPushToken[", StringComparison.Ordinal)) &&
         value.EndsWith(']') && value.Length <= 256);
}
