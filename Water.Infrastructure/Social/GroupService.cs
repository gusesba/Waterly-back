using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Water.Application.Social;
using Water.Domain.Social;
using Water.Domain.Feed;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Social;

public sealed class GroupService(WaterDbContext dbContext, TimeProvider timeProvider) : IGroupService
{
    public const int FreeGroupLimit = 2;
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);

    public async Task<IReadOnlyCollection<GroupSummaryResponse>> GetAllAsync(string userId, CancellationToken token)
    {
        var memberships = await dbContext.GroupMemberships.AsNoTracking().Where(item => item.UserId == userId).ToArrayAsync(token);
        var groupIds = memberships.Select(item => item.GroupId).ToArray();
        var groups = (await dbContext.PrivateGroups.AsNoTracking().Where(item => groupIds.Contains(item.Id)).ToArrayAsync(token)).OrderByDescending(item => item.UpdatedAt).ToArray();
        var counts = await dbContext.GroupMemberships.AsNoTracking().Where(item => groupIds.Contains(item.GroupId)).GroupBy(item => item.GroupId).Select(group => new { group.Key, Count = group.Count() }).ToDictionaryAsync(item => item.Key, item => item.Count, token);
        return groups.Select(group => new GroupSummaryResponse(group.Id, group.Name, group.Description, group.OwnerId == userId, counts[group.Id], group.UpdatedAt)).ToArray();
    }

    public async Task<GroupDetailResponse> GetAsync(string userId, Guid groupId, CancellationToken token)
    {
        var group = await MemberGroupAsync(userId, groupId, token);
        return await MapAsync(group, userId, token);
    }

    public async Task<GroupDetailResponse> CreateAsync(string userId, SaveGroupRequest request, CancellationToken token)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        await EnsureCapacityAsync(userId, token);
        var now = timeProvider.GetUtcNow();
        var group = new PrivateGroup(userId, request.Name, request.Description, now);
        dbContext.PrivateGroups.Add(group);
        dbContext.GroupMemberships.Add(new GroupMembership(group.Id, userId, "owner", now));
        AddGroupJoinedEvent(group, userId, now);
        await dbContext.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return await MapAsync(group, userId, token);
    }

    public async Task<GroupDetailResponse> UpdateAsync(string userId, Guid groupId, SaveGroupRequest request, CancellationToken token)
    {
        var group = await OwnerGroupAsync(userId, groupId, token);
        group.Update(request.Name, request.Description, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(token);
        return await MapAsync(group, userId, token);
    }

    public async Task DeleteAsync(string userId, Guid groupId, CancellationToken token)
    {
        var group = await OwnerGroupAsync(userId, groupId, token);
        dbContext.PrivateGroups.Remove(group);
        await dbContext.SaveChangesAsync(token);
    }

    public async Task<GroupDetailResponse> AddMemberAsync(string userId, Guid groupId, string memberId, CancellationToken token)
    {
        var group = await OwnerGroupAsync(userId, groupId, token);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var alreadyMember = await dbContext.GroupMemberships.AnyAsync(item => item.GroupId == groupId && item.UserId == memberId, token);
        if (!alreadyMember)
        {
            await EnsureCapacityAsync(memberId, token);
            var friendship = await dbContext.Friendships.AsNoTracking().AnyAsync(item => item.AcceptedAt != null &&
                ((item.UserLowId == userId && item.UserHighId == memberId) || (item.UserLowId == memberId && item.UserHighId == userId)), token);
            if (!friendship) throw new GroupConflictException();
            dbContext.GroupMemberships.Add(new GroupMembership(groupId, memberId, "member", timeProvider.GetUtcNow()));
            AddGroupJoinedEvent(group, memberId, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(token);
        }
        await transaction.CommitAsync(token);
        return await MapAsync(group, userId, token);
    }

    public async Task<GroupCapacityResponse> GetCapacityAsync(string userId, CancellationToken token)
    {
        var used = await dbContext.GroupMemberships.AsNoTracking().CountAsync(item => item.UserId == userId, token);
        return new GroupCapacityResponse(used, FreeGroupLimit);
    }

    public async Task<GroupInviteResponse> CreateInviteAsync(string userId, Guid groupId, CancellationToken token)
    {
        await OwnerGroupAsync(userId, groupId, token);
        var now = timeProvider.GetUtcNow();
        var active = await dbContext.GroupInvites.Where(item => item.GroupId == groupId && item.RevokedAt == null).ToArrayAsync(token);
        foreach (var item in active.Where(item => item.ExpiresAt > now)) item.Revoke(now);

        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var invite = new GroupInvite(groupId, userId, HashToken(rawToken), now, now.Add(InviteLifetime));
        dbContext.GroupInvites.Add(invite);
        await dbContext.SaveChangesAsync(token);
        return new GroupInviteResponse(rawToken, invite.ExpiresAt);
    }

    public async Task RevokeInvitesAsync(string userId, Guid groupId, CancellationToken token)
    {
        await OwnerGroupAsync(userId, groupId, token);
        var now = timeProvider.GetUtcNow();
        var active = await dbContext.GroupInvites.Where(item => item.GroupId == groupId && item.RevokedAt == null).ToArrayAsync(token);
        foreach (var item in active.Where(item => item.ExpiresAt > now)) item.Revoke(now);
        await dbContext.SaveChangesAsync(token);
    }

    public async Task<GroupInvitePreviewResponse> GetInviteAsync(string? userId, string inviteToken, CancellationToken token)
    {
        var invite = await AvailableInviteAsync(inviteToken, token);
        var group = await dbContext.PrivateGroups.AsNoTracking().SingleAsync(item => item.Id == invite.GroupId, token);
        var owner = await dbContext.PublicProfiles.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == group.OwnerId, token)
            ?? throw new GroupInviteNotFoundException();
        var memberCount = await dbContext.GroupMemberships.AsNoTracking().CountAsync(item => item.GroupId == group.Id, token);
        var isMember = userId is not null && await dbContext.GroupMemberships.AsNoTracking().AnyAsync(item => item.GroupId == group.Id && item.UserId == userId, token);
        return new GroupInvitePreviewResponse(group.Id, group.Name, owner.DisplayName, memberCount, invite.ExpiresAt, isMember);
    }

    public async Task<GroupDetailResponse> AcceptInviteAsync(string userId, string inviteToken, CancellationToken token)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var invite = await AvailableInviteAsync(inviteToken, token);
        var existing = await dbContext.GroupMemberships.SingleOrDefaultAsync(item => item.GroupId == invite.GroupId && item.UserId == userId, token);
        if (existing is null)
        {
            await EnsureCapacityAsync(userId, token);
            dbContext.GroupMemberships.Add(new GroupMembership(invite.GroupId, userId, "member", timeProvider.GetUtcNow()));
            var invitedGroup = await dbContext.PrivateGroups.SingleAsync(item => item.Id == invite.GroupId, token);
            AddGroupJoinedEvent(invitedGroup, userId, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(token);
        }
        await transaction.CommitAsync(token);
        var group = await dbContext.PrivateGroups.SingleAsync(item => item.Id == invite.GroupId, token);
        return await MapAsync(group, userId, token);
    }

    public async Task RemoveMemberAsync(string userId, Guid groupId, string memberId, CancellationToken token)
    {
        var group = await OwnerGroupAsync(userId, groupId, token);
        if (group.OwnerId == memberId) throw new GroupConflictException();
        var membership = await dbContext.GroupMemberships.SingleOrDefaultAsync(item => item.GroupId == groupId && item.UserId == memberId, token)
            ?? throw new GroupNotFoundException();
        dbContext.GroupMemberships.Remove(membership);
        await dbContext.SaveChangesAsync(token);
    }

    public async Task LeaveAsync(string userId, Guid groupId, CancellationToken token)
    {
        var group = await MemberGroupAsync(userId, groupId, token);
        if (group.OwnerId == userId) throw new GroupConflictException();
        var membership = await dbContext.GroupMemberships.SingleAsync(item => item.GroupId == groupId && item.UserId == userId, token);
        dbContext.GroupMemberships.Remove(membership);
        await dbContext.SaveChangesAsync(token);
    }

    private async Task<PrivateGroup> MemberGroupAsync(string userId, Guid groupId, CancellationToken token)
    {
        var isMember = await dbContext.GroupMemberships.AsNoTracking().AnyAsync(item => item.GroupId == groupId && item.UserId == userId, token);
        if (!isMember) throw new GroupNotFoundException();
        return await dbContext.PrivateGroups.SingleAsync(item => item.Id == groupId, token);
    }

    private async Task<PrivateGroup> OwnerGroupAsync(string userId, Guid groupId, CancellationToken token)
    {
        var group = await dbContext.PrivateGroups.SingleOrDefaultAsync(item => item.Id == groupId && item.OwnerId == userId, token);
        return group ?? throw new GroupNotFoundException();
    }

    private async Task EnsureCapacityAsync(string userId, CancellationToken token)
    {
        if (await dbContext.GroupMemberships.CountAsync(item => item.UserId == userId, token) >= FreeGroupLimit)
            throw new GroupCapacityException();
    }

    private async Task<GroupInvite> AvailableInviteAsync(string inviteToken, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(inviteToken) || inviteToken.Length != 64) throw new GroupInviteNotFoundException();
        var hash = HashToken(inviteToken.ToLowerInvariant());
        var invite = await dbContext.GroupInvites.AsNoTracking().SingleOrDefaultAsync(item => item.TokenHash == hash, token);
        if (invite is null || !invite.IsAvailable(timeProvider.GetUtcNow())) throw new GroupInviteNotFoundException();
        return invite;
    }

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private void AddGroupJoinedEvent(PrivateGroup group, string actorUserId, DateTimeOffset now) => dbContext.FeedEvents.Add(new FeedEvent(
        actorUserId,
        "group-joined",
        "group",
        group.Id.ToString(),
        group.Name,
        $"group-joined:{group.Id}:{actorUserId}",
        now,
        group.Id));

    private async Task<GroupDetailResponse> MapAsync(PrivateGroup group, string userId, CancellationToken token)
    {
        var memberships = (await dbContext.GroupMemberships.AsNoTracking().Where(item => item.GroupId == group.Id).ToArrayAsync(token)).OrderBy(item => item.Role).ThenBy(item => item.JoinedAt).ToArray();
        var ids = memberships.Select(item => item.UserId).ToArray();
        var profiles = await dbContext.PublicProfiles.AsNoTracking().Where(item => ids.Contains(item.UserId)).ToDictionaryAsync(item => item.UserId, token);
        var members = memberships.Where(item => profiles.ContainsKey(item.UserId)).Select(item => new GroupMemberResponse(item.UserId, profiles[item.UserId].Username, profiles[item.UserId].DisplayName, item.Role, item.JoinedAt)).ToArray();
        return new GroupDetailResponse(group.Id, group.Name, group.Description, group.OwnerId == userId, members, group.UpdatedAt);
    }
}
