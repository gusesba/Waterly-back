namespace Water.Infrastructure.Competition;

public sealed class ContestClosureOptions
{
    public const string SectionName = "ContestClosure";

    public bool Enabled { get; init; } = true;
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(15);
    public int BatchSize { get; init; } = 20;
}
