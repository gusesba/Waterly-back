using Water.Domain.Social;

namespace Water.Tests.Unit;

public sealed class FriendshipTests
{
    [Fact]
    public void Friendship_normalizes_pair_and_only_addressee_can_accept()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var friendship = new Friendship("user-b", "user-a", createdAt);
        Assert.Equal("user-a", friendship.UserLowId);
        Assert.Equal("user-b", friendship.UserHighId);
        Assert.Throws<InvalidOperationException>(() => friendship.Accept("user-b", createdAt));
        friendship.Accept("user-a", createdAt.AddMinutes(1));
        Assert.True(friendship.IsAccepted);
    }

    [Fact]
    public void User_cannot_request_themselves() =>
        Assert.Throws<ArgumentException>(() => new Friendship("same", "same", DateTimeOffset.UtcNow));
}
