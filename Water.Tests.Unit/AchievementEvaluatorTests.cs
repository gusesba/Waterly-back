using Water.Domain.Habits;

namespace Water.Tests.Unit;

public sealed class AchievementEvaluatorTests
{
    [Theory]
    [InlineData("completed-days", 1, 0, 1)]
    [InlineData("completed-days", 7, 3, 7)]
    [InlineData("longest-streak", 1, 3, 3)]
    [InlineData("longest-streak", 7, 5, 5)]
    [InlineData("unknown", 7, 7, 0)]
    public void Returns_progress_for_supported_criterion(
        string criterion,
        int completedDays,
        int longestStreak,
        int expected)
    {
        Assert.Equal(
            expected,
            AchievementEvaluator.GetProgress(criterion, completedDays, longestStreak));
    }
}
