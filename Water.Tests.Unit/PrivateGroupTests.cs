using Water.Domain.Social;

namespace Water.Tests.Unit;

public sealed class PrivateGroupTests
{
    [Fact]
    public void Group_trims_content_and_membership_preserves_role()
    {
        var now = DateTimeOffset.UtcNow;
        var group = new PrivateGroup("owner", "  Team Water  ", "  Together  ", now);
        var membership = new GroupMembership(group.Id, "owner", "owner", now);
        Assert.Equal("Team Water", group.Name);
        Assert.Equal("Together", group.Description);
        Assert.Equal("owner", membership.Role);
        Assert.Equal(group.Id, membership.GroupId);
    }
}
