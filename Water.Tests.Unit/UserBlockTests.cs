using Water.Domain.Social;

namespace Water.Tests.Unit;

public sealed class UserBlockTests
{
    [Fact]
    public void Block_requires_distinct_users_and_preserves_creation_time()
    {
        var now = DateTimeOffset.UtcNow;
        var block = new UserBlock("alice", "bob", now);

        Assert.Equal("alice", block.BlockerUserId);
        Assert.Equal("bob", block.BlockedUserId);
        Assert.Equal(now, block.CreatedAt);
        Assert.Throws<ArgumentException>(() => new UserBlock("alice", "alice", now));
    }
}
