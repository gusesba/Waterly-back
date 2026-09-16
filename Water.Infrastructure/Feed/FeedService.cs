using Microsoft.EntityFrameworkCore;
using Water.Application.Feed;
using Water.Domain.Feed;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Feed;

public sealed class FeedService(WaterDbContext dbContext, TimeProvider timeProvider) : IFeedService
{
    private static readonly HashSet<string> ReactionTypes = ["water", "celebrate", "fire"];

    public async Task<FeedPageResponse> GetAsync(string userId, string? cursor, int limit, CancellationToken token)
    {
        limit = Math.Clamp(limit, 1, 30);
        if (cursor is not null && (cursor.Length != 46 || cursor[13] != ':')) throw new InvalidFeedCursorException();

        var query = VisibleEvents(userId).AsNoTracking();
        if (cursor is not null) query = query.Where(item => string.Compare(item.SortKey, cursor) < 0);

        var events = await query.OrderByDescending(item => item.SortKey).Take(limit + 1).ToArrayAsync(token);
        var page = events.Take(limit).ToArray();
        var actorIds = page.Select(item => item.ActorUserId).Distinct().ToArray();
        var eventIds = page.Select(item => item.Id).ToArray();
        var profiles = await dbContext.PublicProfiles.AsNoTracking().Where(item => actorIds.Contains(item.UserId)).ToDictionaryAsync(item => item.UserId, token);
        var reactions = await dbContext.FeedReactions.AsNoTracking().Where(item => eventIds.Contains(item.FeedEventId)).ToArrayAsync(token);
        var items = page.Select(item =>
        {
            profiles.TryGetValue(item.ActorUserId, out var profile);
            return new FeedEventResponse(item.Id, item.EventType,
                new FeedActorResponse(item.ActorUserId, profile?.Username, profile?.DisplayName, item.ActorUserId == userId),
                item.ReferenceId, item.Subject, item.GroupId, item.CreatedAt, Summarize(reactions.Where(reaction => reaction.FeedEventId == item.Id), userId));
        }).ToArray();
        return new FeedPageResponse(items, events.Length > limit ? page[^1].SortKey : null);
    }

    public async Task<FeedReactionSummaryResponse> SetReactionAsync(string userId, Guid eventId, string type, CancellationToken token)
    {
        if (!ReactionTypes.Contains(type)) throw new InvalidFeedReactionException();
        var feedEvent = await VisibleEvents(userId).SingleOrDefaultAsync(item => item.Id == eventId, token) ?? throw new FeedEventNotFoundException();
        if (feedEvent.ActorUserId == userId) throw new OwnFeedEventReactionException();

        var reaction = await dbContext.FeedReactions.SingleOrDefaultAsync(item => item.FeedEventId == eventId && item.UserId == userId, token);
        if (reaction is null) dbContext.FeedReactions.Add(new FeedReaction(eventId, userId, type, timeProvider.GetUtcNow()));
        else reaction.ChangeType(type);
        await dbContext.SaveChangesAsync(token);
        return await GetReactionSummaryAsync(eventId, userId, token);
    }

    public async Task<FeedReactionSummaryResponse> RemoveReactionAsync(string userId, Guid eventId, CancellationToken token)
    {
        var feedEvent = await VisibleEvents(userId).SingleOrDefaultAsync(item => item.Id == eventId, token) ?? throw new FeedEventNotFoundException();
        if (feedEvent.ActorUserId == userId) throw new OwnFeedEventReactionException();
        var reaction = await dbContext.FeedReactions.SingleOrDefaultAsync(item => item.FeedEventId == eventId && item.UserId == userId, token);
        if (reaction is not null)
        {
            dbContext.FeedReactions.Remove(reaction);
            await dbContext.SaveChangesAsync(token);
        }
        return await GetReactionSummaryAsync(eventId, userId, token);
    }

    private IQueryable<FeedEvent> VisibleEvents(string userId) => dbContext.FeedEvents.Where(item =>
        (item.Audience == "friends" && (item.ActorUserId == userId || dbContext.Friendships.Any(friendship =>
            friendship.AcceptedAt != null &&
            ((friendship.UserLowId == userId && friendship.UserHighId == item.ActorUserId) ||
             (friendship.UserHighId == userId && friendship.UserLowId == item.ActorUserId))))) ||
        (item.Audience == "group" && item.GroupId != null && dbContext.GroupMemberships.Any(membership =>
            membership.UserId == userId && membership.GroupId == item.GroupId)));

    private async Task<FeedReactionSummaryResponse> GetReactionSummaryAsync(Guid eventId, string userId, CancellationToken token) =>
        Summarize(await dbContext.FeedReactions.AsNoTracking().Where(item => item.FeedEventId == eventId).ToArrayAsync(token), userId);

    private static FeedReactionSummaryResponse Summarize(IEnumerable<FeedReaction> reactions, string userId)
    {
        var items = reactions.ToArray();
        return new FeedReactionSummaryResponse(
            items.GroupBy(item => item.Type).ToDictionary(group => group.Key, group => group.Count()),
            items.SingleOrDefault(item => item.UserId == userId)?.Type);
    }
}
