using System.ComponentModel.DataAnnotations;

namespace Water.Application.Profiles;

public sealed record UpdatePublicProfileRequest(
    [property: Required, StringLength(20, MinimumLength = 3), RegularExpression("^[a-z0-9_]+$")] string Username,
    [property: Required, StringLength(40, MinimumLength = 2)] string DisplayName,
    [property: StringLength(160)] string? Bio);

public sealed record PublicProfileResponse(
    string Username,
    string DisplayName,
    string? Bio,
    DateTimeOffset UpdatedAt);
