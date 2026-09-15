namespace Water.Application.Habits;

public sealed record DailyClosureCandidate(string UserId, DateOnly CloseThrough);

public interface IDailyClosureService
{
    Task<IReadOnlyCollection<DailyClosureCandidate>> GetDueAsync(
        int batchSize,
        CancellationToken cancellationToken);

    Task CloseAsync(
        DailyClosureCandidate candidate,
        CancellationToken cancellationToken);
}
