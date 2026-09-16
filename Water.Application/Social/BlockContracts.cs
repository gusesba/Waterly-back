namespace Water.Application.Social;

public interface IBlockService
{
    Task<IReadOnlyCollection<SocialProfileResponse>> GetAllAsync(string userId, CancellationToken token);
    Task BlockAsync(string userId, string blockedUserId, CancellationToken token);
    Task UnblockAsync(string userId, string blockedUserId, CancellationToken token);
}

public sealed class BlockConflictException : Exception;
public sealed class BlockNotFoundException : Exception;
