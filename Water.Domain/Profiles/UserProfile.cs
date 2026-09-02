namespace Water.Domain.Profiles;

public sealed class UserProfile
{
    private readonly List<ProfileGoal> _goals = [];

    private UserProfile()
    {
    }

    public UserProfile(
        string userId,
        int age,
        int heightCm,
        decimal weightKg,
        string timeZone,
        IEnumerable<string> goals)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Update(age, heightCm, weightKg, timeZone, goals);
    }

    public Guid Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public int Age { get; private set; }
    public int HeightCm { get; private set; }
    public decimal WeightKg { get; private set; }
    public string TimeZone { get; private set; } = "UTC";
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<ProfileGoal> Goals => _goals;

    public void Update(
        int age,
        int heightCm,
        decimal weightKg,
        string timeZone,
        IEnumerable<string> goals)
    {
        Age = age;
        HeightCm = heightCm;
        WeightKg = weightKg;
        TimeZone = timeZone;
        UpdatedAt = DateTimeOffset.UtcNow;

        _goals.Clear();
        _goals.AddRange(goals.Distinct(StringComparer.Ordinal).Select(goal => new ProfileGoal(Id, goal)));
    }
}
