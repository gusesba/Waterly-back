namespace Water.Application.Habits;

public sealed record AchievementResponse(
    string Code,
    int Progress,
    int Requirement,
    bool IsUnlocked,
    DateTimeOffset? UnlockedAt);
