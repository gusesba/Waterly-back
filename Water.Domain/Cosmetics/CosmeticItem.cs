namespace Water.Domain.Cosmetics;

public sealed class CosmeticItem
{
    private CosmeticItem() { }
    public string Code { get; private set; } = string.Empty;
    public string? RequiredAchievementCode { get; private set; }
    public int SortOrder { get; private set; }
}
