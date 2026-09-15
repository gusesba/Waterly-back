namespace Water.Domain.Habits;

public sealed class UserAchievement
{
    private UserAchievement()
    {
    }

    public UserAchievement(
        string userId,
        string achievementCode,
        DateTimeOffset unlockedAt,
        int ruleVersion)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        AchievementCode = achievementCode;
        UnlockedAt = unlockedAt;
        RuleVersion = ruleVersion;
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string AchievementCode { get; private set; } = string.Empty;
    public DateTimeOffset UnlockedAt { get; private set; }
    public int RuleVersion { get; private set; }
}
