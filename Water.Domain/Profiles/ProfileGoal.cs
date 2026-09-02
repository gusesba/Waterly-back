namespace Water.Domain.Profiles;

public sealed class ProfileGoal
{
    private ProfileGoal()
    {
    }

    public ProfileGoal(Guid profileId, string goal)
    {
        ProfileId = profileId;
        Goal = goal;
    }

    public Guid ProfileId { get; private set; }
    public string Goal { get; private set; } = string.Empty;
}
