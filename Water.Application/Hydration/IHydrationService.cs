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

    Task<TodayHydrationResponse> UpdateEntryAsync(
        string userId,
        Guid entryId,
        UpdateDrinkEntryRequest request,
        CancellationToken cancellationToken);

    Task<TodayHydrationResponse> DeleteEntryAsync(
        string userId,
        Guid entryId,
        Guid clientOperationId,
        CancellationToken cancellationToken);

    Task<HydrationHistoryResponse> GetHistoryAsync(
        string userId,
        int days,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<BeverageResponse>> GetBeveragesAsync(
        string userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<QuickAddSuggestionResponse>> GetSuggestionsAsync(
        string userId,
        string beverageCode,
        CancellationToken cancellationToken);
}

public sealed class HydrationProfileRequiredException : Exception;

public sealed class IdempotencyConflictException : Exception;

public sealed class DrinkEntryNotFoundException : Exception;

public sealed class BeverageNotFoundException : Exception;
