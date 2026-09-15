namespace Water.Application.Progression;

public interface IProgressionService
{
    Task<ProgressionBalanceResponse> GetDropsAsync(
        string userId,
        CancellationToken cancellationToken);

    Task<ProgressionBalanceResponse> GetPrestigeAsync(
        string userId,
        CancellationToken cancellationToken);
}
