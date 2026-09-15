using System.Security.Claims;
using Water.Application.Profiles;

namespace Water.Api.Features.Profiles;

public static class PublicProfileEndpoints
{
    public static IEndpointRouteBuilder MapPublicProfileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/profile").RequireAuthorization().WithTags("Profile");
        group.MapGet("/", GetAsync);
        group.MapPut("/", UpdateAsync);
        return endpoints;
    }

    private static Task<PublicProfileResponse?> GetAsync(ClaimsPrincipal principal, IPublicProfileService service, CancellationToken token) =>
        service.GetAsync(UserId(principal), token);

    private static async Task<IResult> UpdateAsync(UpdatePublicProfileRequest request, ClaimsPrincipal principal, IPublicProfileService service, CancellationToken token)
    {
        try { return TypedResults.Ok(await service.UpdateAsync(UserId(principal), request, token)); }
        catch (PublicProfileUsernameConflictException) { return TypedResults.Conflict(); }
    }

    private static string UserId(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated user has no identifier.");
}
