namespace Water.Application.Habits;

public interface IHabitService
{
    Task<StreakResponse> GetStreakAsync(string userId, CancellationToken cancellationToken);
}
