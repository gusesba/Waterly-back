using System.Security.Claims;
using Water.Application.Profiles;

namespace Water.Api.Features.Profiles;

public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/me")
            .RequireAuthorization()
            .WithTags("Profile");

        group.MapGet("/", GetCurrentUserAsync);
        group.MapPut("/onboarding", CompleteOnboardingAsync);

        return endpoints;
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        IProfileService profileService,
        CancellationToken cancellationToken)
    {
        var identity = GetIdentity(principal);
        var response = await profileService.GetCurrentUserAsync(
            identity.UserId,
            identity.Email,
            cancellationToken);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> CompleteOnboardingAsync(
        CompleteOnboardingRequest request,
        ClaimsPrincipal principal,
        IProfileService profileService,
        CancellationToken cancellationToken)
    {
        var identity = GetIdentity(principal);

        try
        {
            var response = await profileService.CompleteOnboardingAsync(
                identity.UserId,
                identity.Email,
                request,
                cancellationToken);

            return TypedResults.Ok(response);
        }
        catch (ArgumentException exception)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [exception.ParamName ?? "request"] = [exception.Message]
            });
        }
    }

    private static (string UserId, string Email) GetIdentity(ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user has no identifier.");
        var email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.Identity?.Name
            ?? throw new InvalidOperationException("Authenticated user has no email.");

        return (userId, email);
    }
}
