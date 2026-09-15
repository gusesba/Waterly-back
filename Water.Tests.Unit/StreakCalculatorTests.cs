using Water.Domain.Habits;

namespace Water.Tests.Unit;

public sealed class StreakCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 9, 15);

    [Fact]
    public void Completed_today_counts_the_current_run()
    {
        var result = StreakCalculator.Calculate(
            [Today.AddDays(-2), Today.AddDays(-1), Today],
            Today);

        Assert.Equal(3, result.Current);
        Assert.Equal(3, result.Longest);
        Assert.Equal(Today, result.LastCompletedDate);
    }

    [Fact]
    public void Incomplete_today_does_not_break_yesterdays_run()
    {
        var result = StreakCalculator.Calculate(
            [Today.AddDays(-2), Today.AddDays(-1)],
            Today);

        Assert.Equal(2, result.Current);
        Assert.Equal(2, result.Longest);
    }

    [Fact]
    public void Missing_yesterday_breaks_the_current_run_but_preserves_longest()
    {
        var result = StreakCalculator.Calculate(
            [Today.AddDays(-5), Today.AddDays(-4), Today.AddDays(-3)],
            Today);

        Assert.Equal(0, result.Current);
        Assert.Equal(3, result.Longest);
    }

    [Fact]
    public void Recalculation_is_deterministic()
    {
        DateOnly[] dates = [Today.AddDays(-1), Today, Today];

        Assert.Equal(
            StreakCalculator.Calculate(dates, Today),
            StreakCalculator.Calculate(dates, Today));
    }
}
