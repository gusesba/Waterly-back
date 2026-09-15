namespace Water.Domain.Habits;

public sealed class AchievementDefinition
{
    private AchievementDefinition()
    {
    }

    public string Code { get; private set; } = string.Empty;
    public string Criterion { get; private set; } = string.Empty;
    public int Requirement { get; private set; }
    public int SortOrder { get; private set; }
    public int RuleVersion { get; private set; }
}
