namespace Water.Application.Habits;

public sealed record StreakResponse(
    int Current,
    int Longest,
    bool TodayCompleted,
    DateOnly? LastCompletedDate);

public interface IDailyHydrationProjectionService
{
    Task RebuildAsync(string userId, CancellationToken cancellationToken);
}
