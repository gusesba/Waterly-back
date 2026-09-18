namespace Water.Api;

public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimits";

    public int AuthPermitLimit { get; init; } = 10;
    public int SocialPermitLimit { get; init; } = 30;
    public int ContestReadPermitLimit { get; init; } = 60;
    public int ContestWritePermitLimit { get; init; } = 10;
    public int WindowSeconds { get; init; } = 60;
    public int GlobalConcurrencyLimit { get; init; } = 100;
}
