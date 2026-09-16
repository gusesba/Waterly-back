using Microsoft.EntityFrameworkCore;
using Water.Application.Feed;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Feed;

public sealed class FeedService(WaterDbContext dbContext) : IFeedService
{
    public async Task<FeedPageResponse> GetAsync(string userId, string? cursor, int limit, CancellationToken token)
    {
        limit = Math.Clamp(limit, 1, 30);
        if (cursor is not null && (cursor.Length != 46 || cursor[13] != ':')) throw new InvalidFeedCursorException();

        var friendships = await dbContext.Friendships.AsNoTracking()
            .Where(item => item.AcceptedAt != null && (item.UserLowId == userId || item.UserHighId == userId))
            .Select(item => item.UserLowId == userId ? item.UserHighId : item.UserLowId)
            .ToArrayAsync(token);
        var groupIds = await dbContext.GroupMemberships.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.GroupId)
            .ToArrayAsync(token);

        var query = dbContext.FeedEvents.AsNoTracking().Where(item =>
            (item.Audience == "friends" && (item.ActorUserId == userId || friendships.Contains(item.ActorUserId))) ||
            (item.Audience == "group" && item.GroupId != null && groupIds.Contains(item.GroupId.Value)));
        if (cursor is not null) query = query.Where(item => string.Compare(item.SortKey, cursor) < 0);

        var events = await query.OrderByDescending(item => item.SortKey).Take(limit + 1).ToArrayAsync(token);
        var page = events.Take(limit).ToArray();
        var actorIds = page.Select(item => item.ActorUserId).Distinct().ToArray();
        var profiles = await dbContext.PublicProfiles.AsNoTracking().Where(item => actorIds.Contains(item.UserId)).ToDictionaryAsync(item => item.UserId, token);
        var items = page.Select(item =>
        {
            profiles.TryGetValue(item.ActorUserId, out var profile);
            return new FeedEventResponse(item.Id, item.EventType,
                new FeedActorResponse(item.ActorUserId, profile?.Username, profile?.DisplayName, item.ActorUserId == userId),
                item.ReferenceId, item.Subject, item.GroupId, item.CreatedAt);
        }).ToArray();
        return new FeedPageResponse(items, events.Length > limit ? page[^1].SortKey : null);
    }
}
