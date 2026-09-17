using System.ComponentModel.DataAnnotations;

namespace Water.Application.Competition;

public sealed record CreateContestRequest(
    [property: Required, MinLength(3), MaxLength(50)] string Name,
    DateOnly StartsOn,
    int DurationDays);
public sealed record JoinContestRequest(Guid ClientOperationId);
public sealed record ContestCapabilitiesResponse(bool CanManageContests);
public sealed record ContestDailyScoreResponse(DateOnly Date, int DailyTargetMl, int HydrationMl, decimal Score, bool IsFinal);
public sealed record ContestScoreResponse(decimal TotalScore, decimal MaximumScore, IReadOnlyCollection<ContestDailyScoreResponse> Days);
public sealed record ContestResponse(
    Guid Id,
    string Name,
    DateOnly StartsOn,
    DateOnly EndsOn,
    int DurationDays,
    int ScoringRuleVersion,
    int DailyScoreCap,
    string Status,
    int ParticipantCount,
    bool IsParticipant);

public interface IContestService
{
    Task<IReadOnlyCollection<ContestResponse>> GetAllAsync(string userId, CancellationToken token);
    Task<ContestResponse> GetAsync(string userId, Guid contestId, CancellationToken token);
    Task<ContestResponse> CreateAsync(string userId, CreateContestRequest request, CancellationToken token);
    Task<ContestResponse> JoinAsync(string userId, Guid contestId, Guid clientOperationId, CancellationToken token);
}

public interface IContestScoreService
{
    Task<ContestScoreResponse> GetAsync(string userId, Guid contestId, CancellationToken token);
    Task FinalizeThroughAsync(string userId, DateOnly closeThrough, CancellationToken token);
}

public sealed class ContestNotFoundException : Exception;
public sealed class ContestConflictException : Exception;
public sealed class ContestValidationException : Exception;
