namespace Water.Application.Habits;

public sealed record StreakResponse(
    int Current,
    int Longest,
    bool TodayCompleted,
    DateOnly? LastCompletedDate);
