using System.ComponentModel.DataAnnotations;

namespace Water.Application.Cosmetics;

public sealed record CosmeticResponse(string Code, bool IsOwned, bool IsSelected, string? RequiredAchievementCode);
public sealed record CharacterLoadoutResponse(string CharacterCode, string AuraCode, IReadOnlyCollection<CosmeticResponse> Auras);
public sealed record UpdateCharacterLoadoutRequest([property: Required, MaxLength(32)] string AuraCode);

public interface ICosmeticService
{
    Task<CharacterLoadoutResponse> GetAsync(string userId, CancellationToken cancellationToken);
    Task<CharacterLoadoutResponse> UpdateAsync(string userId, string auraCode, CancellationToken cancellationToken);
}

public sealed class CosmeticNotAvailableException : Exception;
