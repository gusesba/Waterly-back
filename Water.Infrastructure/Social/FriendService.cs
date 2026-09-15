using Microsoft.EntityFrameworkCore;
using Water.Application.Social;
using Water.Domain.Profiles;
using Water.Domain.Social;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Social;

public sealed class FriendService(WaterDbContext dbContext, TimeProvider timeProvider) : IFriendService
{
    public async Task<IReadOnlyCollection<SocialProfileResponse>> SearchAsync(string userId, string query, CancellationToken token)
    {
        var normalized = query.Trim().ToUpperInvariant();
        if (normalized.Length < 3) return [];
        var profiles = await dbContext.PublicProfiles.AsNoTracking()
            .Where(item => item.UserId != userId && item.NormalizedUsername.StartsWith(normalized))
            .OrderBy(item => item.NormalizedUsername).Take(10).ToArrayAsync(token);
        return await MapProfilesAsync(userId, profiles, token);
    }

    public async Task<IReadOnlyCollection<SocialProfileResponse>> GetFriendsAsync(string userId, CancellationToken token)
    {
        var relations = await Relations(userId).Where(item => item.AcceptedAt != null).AsNoTracking().ToArrayAsync(token);
        var ids = relations.Select(item => item.OtherUserId(userId)).ToArray();
        var profiles = await dbContext.PublicProfiles.AsNoTracking().Where(item => ids.Contains(item.UserId)).OrderBy(item => item.Username).ToArrayAsync(token);
        return profiles.Select(item => Map(item, "friends")).ToArray();
    }

    public async Task<IReadOnlyCollection<FriendRequestResponse>> GetRequestsAsync(string userId, CancellationToken token)
    {
        var relations = await Relations(userId).Where(item => item.AcceptedAt == null).AsNoTracking().OrderByDescending(item => item.CreatedAt).ToArrayAsync(token);
        var ids = relations.Select(item => item.OtherUserId(userId)).ToArray();
        var profiles = await dbContext.PublicProfiles.AsNoTracking().Where(item => ids.Contains(item.UserId)).ToDictionaryAsync(item => item.UserId, token);
        return relations.Where(item => profiles.ContainsKey(item.OtherUserId(userId))).Select(item => new FriendRequestResponse(
            item.Id, Map(profiles[item.OtherUserId(userId)], item.RequestedByUserId == userId ? "outgoing" : "incoming"),
            item.RequestedByUserId == userId ? "outgoing" : "incoming", item.CreatedAt)).ToArray();
    }

    public async Task<FriendRequestResponse> RequestAsync(string userId, string username, CancellationToken token)
    {
        var target = await dbContext.PublicProfiles.SingleOrDefaultAsync(item => item.NormalizedUsername == username.Trim().ToUpperInvariant(), token)
            ?? throw new SocialNotFoundException();
        if (target.UserId == userId) throw new SocialConflictException();
        var relation = await FindPairAsync(userId, target.UserId, token);
        if (relation is null)
        {
            relation = new Friendship(userId, target.UserId, timeProvider.GetUtcNow());
            dbContext.Friendships.Add(relation);
        }
        else if (!relation.IsAccepted && relation.RequestedByUserId != userId)
        {
            relation.Accept(userId, timeProvider.GetUtcNow());
        }
        await dbContext.SaveChangesAsync(token);
        var direction = relation.IsAccepted ? "friends" : "outgoing";
        return new FriendRequestResponse(relation.Id, Map(target, direction), direction, relation.CreatedAt);
    }

    public async Task AcceptAsync(string userId, Guid requestId, CancellationToken token)
    {
        var relation = await dbContext.Friendships.SingleOrDefaultAsync(item => item.Id == requestId && (item.UserLowId == userId || item.UserHighId == userId), token)
            ?? throw new SocialNotFoundException();
        try { relation.Accept(userId, timeProvider.GetUtcNow()); }
        catch (InvalidOperationException) { throw new SocialConflictException(); }
        await dbContext.SaveChangesAsync(token);
    }

    public async Task RemoveRequestAsync(string userId, Guid requestId, CancellationToken token)
    {
        var relation = await dbContext.Friendships.SingleOrDefaultAsync(item => item.Id == requestId && item.AcceptedAt == null && (item.UserLowId == userId || item.UserHighId == userId), token)
            ?? throw new SocialNotFoundException();
        dbContext.Friendships.Remove(relation);
        await dbContext.SaveChangesAsync(token);
    }

    public async Task RemoveFriendAsync(string userId, string otherUserId, CancellationToken token)
    {
        var relation = await FindPairAsync(userId, otherUserId, token);
        if (relation is null || !relation.IsAccepted) throw new SocialNotFoundException();
        dbContext.Friendships.Remove(relation);
        await dbContext.SaveChangesAsync(token);
    }

    private IQueryable<Friendship> Relations(string userId) => dbContext.Friendships.Where(item => item.UserLowId == userId || item.UserHighId == userId);
    private Task<Friendship?> FindPairAsync(string first, string second, CancellationToken token)
    {
        var low = string.CompareOrdinal(first, second) < 0 ? first : second;
        var high = first == low ? second : first;
        return dbContext.Friendships.SingleOrDefaultAsync(item => item.UserLowId == low && item.UserHighId == high, token);
    }

    private async Task<IReadOnlyCollection<SocialProfileResponse>> MapProfilesAsync(string userId, PublicProfile[] profiles, CancellationToken token)
    {
        var relations = await Relations(userId).AsNoTracking().ToArrayAsync(token);
        return profiles.Select(profile =>
        {
            var relation = relations.SingleOrDefault(item => item.Includes(profile.UserId));
            var state = relation is null ? "none" : relation.IsAccepted ? "friends" : relation.RequestedByUserId == userId ? "outgoing" : "incoming";
            return Map(profile, state);
        }).ToArray();
    }

    private static SocialProfileResponse Map(PublicProfile profile, string relationship) =>
        new(profile.UserId, profile.Username, profile.DisplayName, profile.Bio, relationship);
}
