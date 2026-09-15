namespace Water.Domain.Habits;

public static class AchievementEvaluator
{
    public static int GetProgress(string criterion, int completedDays, int longestStreak) =>
        criterion switch
        {
            "completed-days" => completedDays,
            "longest-streak" => longestStreak,
            _ => 0
        };
}
