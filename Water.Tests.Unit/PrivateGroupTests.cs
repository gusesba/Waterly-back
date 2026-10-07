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

    [Fact]
    public void Invite_can_be_revoked_only_once_and_expires()
    {
        var now = DateTimeOffset.UtcNow;
        var invite = new GroupInvite(Guid.NewGuid(), "owner", "hash", now, now.AddDays(7));
        Assert.True(invite.IsAvailable(now));
        invite.Revoke(now.AddMinutes(1));
        invite.Revoke(now.AddMinutes(2));
        Assert.False(invite.IsAvailable(now.AddMinutes(1)));
        Assert.Equal(now.AddMinutes(1), invite.RevokedAt);
        var expired = new GroupInvite(Guid.NewGuid(), "owner", "other-hash", now, now.AddDays(7));
        Assert.False(expired.IsAvailable(now.AddDays(7)));
    }

    [Fact]
    public void Ownership_can_be_transferred_and_membership_promoted()
    {
        var now = DateTimeOffset.UtcNow;
        var group = new PrivateGroup("old-owner", "Team", null, now);
        var membership = new GroupMembership(group.Id, "new-owner", "member", now);

        group.TransferOwnership("new-owner", now.AddMinutes(1));
        membership.PromoteToOwner();

        Assert.Equal("new-owner", group.OwnerId);
        Assert.Equal("owner", membership.Role);
    }
}
