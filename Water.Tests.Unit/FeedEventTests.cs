using Water.Domain.Feed;

namespace Water.Tests.Unit;

public sealed class FeedEventTests
{
    [Fact]
    public void Event_keeps_a_stable_sort_key_and_reference()
    {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(1_789_500_000_123);
        var item = new FeedEvent("user", "achievement-unlocked", "friends", "first-goal", "first-goal", "key", now);
        Assert.StartsWith("1789500000123:", item.SortKey);
        Assert.Equal(46, item.SortKey.Length);
        Assert.Equal("first-goal", item.ReferenceId);
    }
}
