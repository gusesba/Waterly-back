namespace Water.Application.Habits;

public interface IAchievementService
{
    Task<IReadOnlyCollection<AchievementResponse>> GetAchievementsAsync(
        string userId,
        CancellationToken cancellationToken);
}
