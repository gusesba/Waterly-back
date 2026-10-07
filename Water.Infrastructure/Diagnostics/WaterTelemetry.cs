using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Water.Infrastructure.Diagnostics;

public static class WaterTelemetry
{
    public const string Name = "Waterly";
    public static readonly ActivitySource Activities = new(Name);
    public static readonly Meter Meter = new(Name, "1.0");
    public static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>("waterly.http.duration", "s");
    private static readonly Counter<long> HydrationReplays = Meter.CreateCounter<long>("waterly.hydration.replays");
    private static readonly Counter<long> JobRuns = Meter.CreateCounter<long>("waterly.job.runs");
    private static readonly Histogram<double> JobDuration = Meter.CreateHistogram<double>("waterly.job.duration", "s");
    private static readonly ConcurrentDictionary<string, DateTimeOffset> Successes = new();
    private static readonly ObservableGauge<double> JobLastSuccess = Meter.CreateObservableGauge(
        "waterly.job.last_success", () => Successes.Select(pair => new Measurement<double>(pair.Value.ToUnixTimeSeconds(),
            new KeyValuePair<string, object?>("job.name", pair.Key))), "s");

    public static void RecordJob(string job, bool succeeded, TimeSpan duration)
    {
        var tags = new TagList { { "job.name", job }, { "outcome", succeeded ? "success" : "failure" } };
        JobRuns.Add(1, tags);
        JobDuration.Record(duration.TotalSeconds, tags);
        if (succeeded) Successes[job] = DateTimeOffset.UtcNow;
    }

    public static void RecordHydrationReplay(string operation) => HydrationReplays.Add(1,
        new KeyValuePair<string, object?>("operation", operation));
}
