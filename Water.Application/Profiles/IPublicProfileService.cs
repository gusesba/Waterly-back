namespace Water.Application.Profiles;

public interface IPublicProfileService
{
    Task<PublicProfileResponse?> GetAsync(string userId, CancellationToken cancellationToken);
    Task<PublicProfileResponse> UpdateAsync(string userId, UpdatePublicProfileRequest request, CancellationToken cancellationToken);
}

public sealed class PublicProfileUsernameConflictException : Exception;
