using System.Security.Claims;
using Water.Application.Notifications;

namespace Water.Api.Features.Notifications;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints, bool requireRateLimiting = true)
    {
        var group = endpoints.MapGroup("/api/v1/notifications/installations").RequireAuthorization().WithTags("Notifications");
        if (requireRateLimiting) group.RequireRateLimiting("social");
        group.MapPut("/{installationId:guid}", UpsertAsync);
        group.MapDelete("/{installationId:guid}", DisableAsync);
        return endpoints;
    }

    private static async Task<IResult> UpsertAsync(Guid installationId, UpsertDeviceInstallationRequest request, ClaimsPrincipal principal, IDeviceInstallationService service, CancellationToken token)
    {
        try { return TypedResults.Ok(await service.UpsertAsync(UserId(principal), installationId, request, token)); }
        catch (InvalidDeviceInstallationException) { return Problem(StatusCodes.Status400BadRequest, "Invalid device installation.", "invalid_device_installation"); }
        catch (DeviceInstallationConflictException) { return Problem(StatusCodes.Status409Conflict, "Device installation belongs to another account.", "device_installation_conflict"); }
    }

    private static async Task<IResult> DisableAsync(Guid installationId, ClaimsPrincipal principal, IDeviceInstallationService service, CancellationToken token)
    {
        await service.DisableAsync(UserId(principal), installationId, token);
        return TypedResults.NoContent();
    }

    private static IResult Problem(int status, string title, string code) =>
        TypedResults.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["code"] = code });

    private static string UserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated user has no identifier.");
}
