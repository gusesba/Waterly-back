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
        Assert.Equal(100, contest.DailyScoreCap);
        Assert.Equal("upcoming", contest.Status(new DateOnly(2026, 9, 15)));
        Assert.Equal("active", contest.Status(new DateOnly(2026, 9, 16)));
        Assert.Equal("ended", contest.Status(new DateOnly(2026, 9, 23)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Contest("Invalid", new DateOnly(2026, 9, 16), 8, DateTimeOffset.UtcNow));
    }
}
