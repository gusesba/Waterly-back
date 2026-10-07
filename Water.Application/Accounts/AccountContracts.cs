namespace Water.Application.Accounts;

public sealed record DeleteAccountRequest(string CurrentPassword);

public interface IAccountPrivacyService
{
    Task<byte[]> ExportAsync(string userId, string email, CancellationToken cancellationToken);
    Task DeleteAsync(string userId, string currentPassword, CancellationToken cancellationToken);
}

public sealed class InvalidAccountPasswordException : Exception;
