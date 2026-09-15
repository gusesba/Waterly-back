using Microsoft.EntityFrameworkCore;
using Water.Application.Social;
using Water.Domain.Social;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Social;

public sealed class GroupService(WaterDbContext dbContext, TimeProvider timeProvider) : IGroupService
{
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
        var now = timeProvider.GetUtcNow();
        var group = new PrivateGroup(userId, request.Name, request.Description, now);
        dbContext.PrivateGroups.Add(group);
        dbContext.GroupMemberships.Add(new GroupMembership(group.Id, userId, "owner", now));
        await dbContext.SaveChangesAsync(token);
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
        var alreadyMember = await dbContext.GroupMemberships.AnyAsync(item => item.GroupId == groupId && item.UserId == memberId, token);
        if (!alreadyMember)
        {
            var friendship = await dbContext.Friendships.AsNoTracking().AnyAsync(item => item.AcceptedAt != null &&
                ((item.UserLowId == userId && item.UserHighId == memberId) || (item.UserLowId == memberId && item.UserHighId == userId)), token);
            if (!friendship) throw new GroupConflictException();
            dbContext.GroupMemberships.Add(new GroupMembership(groupId, memberId, "member", timeProvider.GetUtcNow()));
            await dbContext.SaveChangesAsync(token);
        }
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

    private async Task<GroupDetailResponse> MapAsync(PrivateGroup group, string userId, CancellationToken token)
    {
        var memberships = (await dbContext.GroupMemberships.AsNoTracking().Where(item => item.GroupId == group.Id).ToArrayAsync(token)).OrderBy(item => item.Role).ThenBy(item => item.JoinedAt).ToArray();
        var ids = memberships.Select(item => item.UserId).ToArray();
        var profiles = await dbContext.PublicProfiles.AsNoTracking().Where(item => ids.Contains(item.UserId)).ToDictionaryAsync(item => item.UserId, token);
        var members = memberships.Where(item => profiles.ContainsKey(item.UserId)).Select(item => new GroupMemberResponse(item.UserId, profiles[item.UserId].Username, profiles[item.UserId].DisplayName, item.Role, item.JoinedAt)).ToArray();
        return new GroupDetailResponse(group.Id, group.Name, group.Description, group.OwnerId == userId, members, group.UpdatedAt);
    }
}
