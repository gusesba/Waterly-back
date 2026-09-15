namespace Water.Infrastructure.Habits;

public sealed class DailyClosureOptions
{
    public const string SectionName = "DailyClosure";

    public bool Enabled { get; init; } = true;
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(15);
    public int BatchSize { get; init; } = 100;
}
