namespace Water.Application.Social;

public sealed record GroupLeaderboardEntryResponse(
    int Position, string? Username, string? DisplayName, string AuraCode,
    decimal TotalScore, int CompletedDays, bool IsTied, bool IsCurrentUser);

public sealed record GroupLeaderboardResponse(
    IReadOnlyCollection<GroupLeaderboardEntryResponse> Entries, int TotalCount,
    int Page, int PageSize, DateOnly StartsOn, DateOnly EndsOn, int RuleVersion);

public interface IGroupLeaderboardService
{
    Task<GroupLeaderboardResponse> GetAsync(string userId, Guid groupId, int page, int pageSize, CancellationToken token);
}

public sealed class GroupLeaderboardValidationException : Exception;
