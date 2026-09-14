namespace Water.Application.Hydration;

public interface IHydrationService
{
    Task<TodayHydrationResponse> GetTodayAsync(
        string userId,
        CancellationToken cancellationToken);

    Task<TodayHydrationResponse> AddEntryAsync(
        string userId,
        AddDrinkEntryRequest request,
        CancellationToken cancellationToken);
}

public sealed class HydrationProfileRequiredException : Exception;

public sealed class IdempotencyConflictException : Exception;
