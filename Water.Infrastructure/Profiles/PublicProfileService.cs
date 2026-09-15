using Microsoft.EntityFrameworkCore;
using Water.Application.Profiles;
using Water.Domain.Profiles;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Profiles;

public sealed class PublicProfileService(WaterDbContext dbContext) : IPublicProfileService
{
    public async Task<PublicProfileResponse?> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.PublicProfiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        return profile is null ? null : Map(profile);
    }

    public async Task<PublicProfileResponse> UpdateAsync(string userId, UpdatePublicProfileRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim().ToLowerInvariant();
        var normalized = username.ToUpperInvariant();
        if (await dbContext.PublicProfiles.AnyAsync(
            item => item.NormalizedUsername == normalized && item.UserId != userId,
            cancellationToken)) throw new PublicProfileUsernameConflictException();

        var profile = await dbContext.PublicProfiles.SingleOrDefaultAsync(
            item => item.UserId == userId, cancellationToken);
        if (profile is null)
        {
            profile = new PublicProfile(userId, username, request.DisplayName, request.Bio);
            dbContext.PublicProfiles.Add(profile);
        }
        else profile.Update(username, request.DisplayName, request.Bio);

        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { throw new PublicProfileUsernameConflictException(); }
        return Map(profile);
    }

    private static PublicProfileResponse Map(PublicProfile profile) =>
        new(profile.Username, profile.DisplayName, profile.Bio, profile.UpdatedAt);
}
