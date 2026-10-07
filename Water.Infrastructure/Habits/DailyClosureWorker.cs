using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Water.Application.Habits;
using System.Diagnostics;
using Water.Infrastructure.Diagnostics;

namespace Water.Infrastructure.Habits;

public sealed class DailyClosureWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<DailyClosureOptions> options,
    ILogger<DailyClosureWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;

        await TryRunOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(options.Value.Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await TryRunOnceAsync(stoppingToken);
        }
    }

    private async Task TryRunOnceAsync(CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        using var activity = WaterTelemetry.Activities.StartActivity("job.daily-closure");
        try
        {
            var succeeded = await RunOnceAsync(cancellationToken);
            WaterTelemetry.RecordJob("daily-closure", succeeded, Stopwatch.GetElapsedTime(started));
            if (!succeeded) activity?.SetStatus(ActivityStatusCode.Error);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            WaterTelemetry.RecordJob("daily-closure", false, Stopwatch.GetElapsedTime(started));
            activity?.SetStatus(ActivityStatusCode.Error);
            logger.LogError(exception, "Daily closure batch failed and will be retried on the next interval");
        }
    }

    internal async Task<bool> RunOnceAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        IReadOnlyCollection<DailyClosureCandidate> candidates;
        using (var scope = scopeFactory.CreateScope())
        {
            candidates = await scope.ServiceProvider.GetRequiredService<IDailyClosureService>()
                .GetDueAsync(options.Value.BatchSize, cancellationToken);
        }

        var completed = 0;
        var failed = 0;
        foreach (var candidate in candidates)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IDailyClosureService>()
                    .CloseAsync(candidate, cancellationToken);
                completed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failed++;
                logger.LogError(exception, "Daily closure failed for user {UserId} through {CloseThrough}",
                    candidate.UserId, candidate.CloseThrough);
            }
        }

        logger.LogInformation(
            "Daily closure finished: {Completed} completed, {Failed} failed in {ElapsedMilliseconds} ms",
            completed, failed, (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);
        return failed == 0;
    }
}
