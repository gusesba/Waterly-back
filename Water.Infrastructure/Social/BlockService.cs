using System.Data;
using Microsoft.EntityFrameworkCore;
using Water.Application.Social;
using Water.Domain.Social;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Social;

public sealed class BlockService(WaterDbContext dbContext, TimeProvider timeProvider) : IBlockService
{
    public async Task<IReadOnlyCollection<SocialProfileResponse>> GetAllAsync(string userId, CancellationToken token)
    {
        var blockedIds = await dbContext.UserBlocks.AsNoTracking()
            .Where(item => item.BlockerUserId == userId)
            .Select(item => item.BlockedUserId)
            .ToArrayAsync(token);
        var profiles = await dbContext.PublicProfiles.AsNoTracking()
            .Where(item => blockedIds.Contains(item.UserId))
            .OrderBy(item => item.Username)
            .ToArrayAsync(token);
        return profiles.Select(item => new SocialProfileResponse(item.UserId, item.Username, item.DisplayName, item.Bio, "blocked")).ToArray();
    }

    public async Task BlockAsync(string userId, string blockedUserId, CancellationToken token)
    {
        if (userId == blockedUserId) throw new BlockConflictException();
        if (!await dbContext.PublicProfiles.AsNoTracking().AnyAsync(item => item.UserId == blockedUserId, token)) throw new BlockNotFoundException();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var exists = await dbContext.UserBlocks.AnyAsync(item => item.BlockerUserId == userId && item.BlockedUserId == blockedUserId, token);
        if (!exists) dbContext.UserBlocks.Add(new UserBlock(userId, blockedUserId, timeProvider.GetUtcNow()));
        var friendship = await Pair(userId, blockedUserId).SingleOrDefaultAsync(token);
        if (friendship is not null) dbContext.Friendships.Remove(friendship);
        await dbContext.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }

    public async Task UnblockAsync(string userId, string blockedUserId, CancellationToken token)
    {
        var block = await dbContext.UserBlocks.SingleOrDefaultAsync(item => item.BlockerUserId == userId && item.BlockedUserId == blockedUserId, token);
        if (block is null) return;
        dbContext.UserBlocks.Remove(block);
        await dbContext.SaveChangesAsync(token);
    }

    private IQueryable<Friendship> Pair(string first, string second)
    {
        var low = string.CompareOrdinal(first, second) < 0 ? first : second;
        var high = first == low ? second : first;
        return dbContext.Friendships.Where(item => item.UserLowId == low && item.UserHighId == high);
    }
}
