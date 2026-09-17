namespace Water.Application.Progression;

public sealed record ProgressionEntryResponse(
    Guid Id,
    int Amount,
    string EntryType,
    string ReferenceType,
    string ReferenceId,
    string? ReferenceLabel,
    DateTimeOffset CreatedAt);

public sealed record ProgressionBalanceResponse(
    int Balance,
    IReadOnlyCollection<ProgressionEntryResponse> Entries);
