using System.ComponentModel.DataAnnotations;

namespace Water.Application.Hydration;

public sealed record AddDrinkEntryRequest(
    Guid ClientEntryId,
    [property: Range(1, 2000)] int VolumeMl,
    DateTimeOffset OccurredAt,
    [property: Required, StringLength(50)] string TimeZone,
    [property: Required, StringLength(32)] string BeverageCode = "water",
    [property: Required, StringLength(16), RegularExpression("^(unknown|quick-add|custom)$")] string InputMethod = "unknown");

public sealed record UpdateDrinkEntryRequest(
    [property: Range(1, 2000)] int VolumeMl,
    [property: Required, StringLength(32)] string BeverageCode = "water",
    Guid ClientOperationId = default);

public sealed record TodayHydrationResponse(
    DateOnly Date,
    int DailyTargetMl,
    int ConsumedMl,
    double Progress,
    IReadOnlyCollection<DrinkEntryResponse> Entries);

public sealed record DrinkEntryResponse(
    Guid Id,
    Guid ClientEntryId,
    int VolumeMl,
    int HydrationMl,
    string BeverageCode,
    DateTimeOffset OccurredAt,
    string TimeZone,
    string Source);

public sealed record BeverageResponse(
    string Code,
    decimal HydrationFactor,
    int TotalVolumeMl);

public sealed record QuickAddSuggestionResponse(string BeverageCode, int VolumeMl);

public sealed record HydrationHistoryResponse(
    IReadOnlyCollection<HydrationHistoryDayResponse> Days);

public sealed record HydrationHistoryDayResponse(
    DateOnly Date,
    int DailyTargetMl,
    int ConsumedMl,
    double Progress,
    IReadOnlyCollection<DrinkEntryResponse> Entries);
