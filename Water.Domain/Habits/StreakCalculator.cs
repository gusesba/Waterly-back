namespace Water.Domain.Habits;

public static class StreakCalculator
{
    public static StreakResult Calculate(IEnumerable<DateOnly> completedDates, DateOnly today)
    {
        var dates = completedDates.Distinct().Order().ToArray();
        var completed = dates.ToHashSet();
        var anchor = completed.Contains(today) ? today : today.AddDays(-1);
        var current = 0;
        while (completed.Contains(anchor))
        {
            current++;
            anchor = anchor.AddDays(-1);
        }

        var longest = 0;
        var run = 0;
        DateOnly? previous = null;
        foreach (var date in dates)
        {
            run = previous is not null && date == previous.Value.AddDays(1) ? run + 1 : 1;
            longest = Math.Max(longest, run);
            previous = date;
        }

        return new StreakResult(current, longest, dates.LastOrDefault() == default ? null : dates[^1]);
    }
}

public sealed record StreakResult(int Current, int Longest, DateOnly? LastCompletedDate);
