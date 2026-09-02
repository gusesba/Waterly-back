namespace Water.Application.Profiles;

public interface IProfileService
{
    Task<CurrentUserResponse> GetCurrentUserAsync(string userId, string email, CancellationToken cancellationToken);
    Task<CurrentUserResponse> CompleteOnboardingAsync(
        string userId,
        string email,
        CompleteOnboardingRequest request,
        CancellationToken cancellationToken);
}
