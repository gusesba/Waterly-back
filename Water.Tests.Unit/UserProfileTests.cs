using Water.Domain.Profiles;

namespace Water.Tests.Unit;

public sealed class UserProfileTests
{
    [Fact]
    public void Update_replaces_goals_without_duplicates()
    {
        var profile = new UserProfile("user-1", 25, 170, 70, "UTC", ["habit"]);

        profile.Update(26, 171, 71, "America/Sao_Paulo", ["energy", "energy"]);

        Assert.Equal(26, profile.Age);
        Assert.Equal("America/Sao_Paulo", profile.TimeZone);
        Assert.Collection(profile.Goals, goal => Assert.Equal("energy", goal.Goal));
    }
}
