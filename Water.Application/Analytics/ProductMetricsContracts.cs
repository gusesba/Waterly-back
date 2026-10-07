namespace Water.Application.Analytics;

public sealed record RetentionMetricResponse(int Day, string Segment, int EligibleUsers, int RetainedUsers, decimal? Rate);
public sealed record DropsFlowMetricResponse(string EntryType, string ReferenceType, long Earned, long Spent);
public sealed record AcquisitionMetricsResponse(
    int FirstRecordAccounts, double? AverageTimeToFirstRecordSeconds,
    int QuickAddRecords, int CustomRecords, int UnknownInputRecords, decimal? RecordsPerActiveUserDay,
    int CreatedInvites, long InvitePreviews, long InviteAcceptances);
public sealed record ProductMetricsResponse(
    DateTimeOffset GeneratedAt, DateOnly CohortStartsOn, string CohortDefinition,
    int Accounts, int ActiveGroups, int AcceptedFriendships, int DrinkEntries, int Reactions,
    int ContestParticipants, int CompletedContests, int Achievements,
    long DropsEarned, long DropsSpent, long PrestigeEarned,
    int PendingNotifications, int FailedNotifications,
    IReadOnlyCollection<RetentionMetricResponse> Retention, AcquisitionMetricsResponse Acquisition,
    int ActiveStreaks, int LongestStreak, IReadOnlyCollection<DropsFlowMetricResponse> DropsFlow);
public interface IProductMetricsService
{
    Task<ProductMetricsResponse> GetAsync(CancellationToken token);
}
