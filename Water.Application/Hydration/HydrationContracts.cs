using System.ComponentModel.DataAnnotations;

namespace Water.Application.Hydration;

public sealed record AddDrinkEntryRequest(
    Guid ClientEntryId,
    [property: Range(1, 2000)] int VolumeMl,
    DateTimeOffset OccurredAt,
    [property: Required, StringLength(50)] string TimeZone);

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
    DateTimeOffset OccurredAt,
    string TimeZone,
    string Source);
