using Water.Domain.Competition;

namespace Water.Tests.Unit;

public sealed class ContestTests
{
    [Fact]
    public void Contest_freezes_rules_dates_and_derives_status()
    {
        var contest = new Contest("  Seven days  ", new DateOnly(2026, 9, 16), 7, DateTimeOffset.UtcNow);

        Assert.Equal("Seven days", contest.Name);
        Assert.Equal(new DateOnly(2026, 9, 23), contest.EndsOn);
        Assert.Equal(1, contest.ScoringRuleVersion);
        Assert.Equal(1, contest.RewardRuleVersion);
        Assert.Equal(100, contest.DailyScoreCap);
        Assert.Equal("upcoming", contest.Status(new DateOnly(2026, 9, 15)));
        Assert.Equal("active", contest.Status(new DateOnly(2026, 9, 16)));
        Assert.Equal("ended", contest.Status(new DateOnly(2026, 9, 23)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Contest("Invalid", new DateOnly(2026, 9, 16), 8, DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1000, 50)]
    [InlineData(2000, 100)]
    [InlineData(3000, 100)]
    public void Daily_score_is_linear_and_capped(int hydrationMl, decimal expected)
    {
        Assert.Equal(expected, ContestDailyScore.Calculate(2000, hydrationMl, 100));
    }

    [Fact]
    public void Final_result_can_be_anonymized_without_changing_score()
    {
        var result = new ContestResult(Guid.NewGuid(), "user", 2, 650, 7, false, "person", "Person", DateTimeOffset.UtcNow);

        result.Anonymize();

        Assert.Null(result.UserId);
        Assert.Null(result.Username);
        Assert.Null(result.DisplayName);
        Assert.Equal(2, result.Position);
        Assert.Equal(650, result.TotalScore);
    }
}
