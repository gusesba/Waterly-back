using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Water.Application.Competition;
using System.Diagnostics;
using Water.Infrastructure.Diagnostics;

namespace Water.Infrastructure.Competition;

public sealed class ContestClosureWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ContestClosureOptions> options,
    ILogger<ContestClosureWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        await TryRunOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(options.Value.Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken)) await TryRunOnceAsync(stoppingToken);
    }

    private async Task TryRunOnceAsync(CancellationToken token)
    {
        var started = Stopwatch.GetTimestamp();
        using var activity = WaterTelemetry.Activities.StartActivity("job.contest-closure");
        try
        {
            var succeeded = await RunOnceAsync(token);
            WaterTelemetry.RecordJob("contest-closure", succeeded, Stopwatch.GetElapsedTime(started));
            if (!succeeded) activity?.SetStatus(ActivityStatusCode.Error);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            WaterTelemetry.RecordJob("contest-closure", false, Stopwatch.GetElapsedTime(started));
            activity?.SetStatus(ActivityStatusCode.Error);
            logger.LogError(exception, "Contest closure batch failed and will be retried");
        }
    }

    internal async Task<bool> RunOnceAsync(CancellationToken token)
    {
        var succeeded = true;
        IReadOnlyCollection<Guid> contestIds;
        using (var scope = scopeFactory.CreateScope())
            contestIds = await scope.ServiceProvider.GetRequiredService<IContestFinalizationService>().GetDueAsync(options.Value.BatchSize, token);

        foreach (var contestId in contestIds)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IContestFinalizationService>().FinalizeAsync(contestId, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception exception) { succeeded = false; logger.LogError(exception, "Contest closure failed for {ContestId}", contestId); }
        }

        IReadOnlyCollection<Guid> rewardContestIds;
        using (var scope = scopeFactory.CreateScope())
            rewardContestIds = await scope.ServiceProvider.GetRequiredService<IContestRewardService>().GetDueAsync(options.Value.BatchSize, token);
        foreach (var contestId in rewardContestIds)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IContestRewardService>().GrantAsync(contestId, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception exception) { succeeded = false; logger.LogError(exception, "Contest rewards failed for {ContestId}", contestId); }
        }
        return succeeded;
    }
}
