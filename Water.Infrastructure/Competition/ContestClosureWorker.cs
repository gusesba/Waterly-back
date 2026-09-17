using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Water.Application.Competition;

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
        try { await RunOnceAsync(token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception) { logger.LogError(exception, "Contest closure batch failed and will be retried"); }
    }

    internal async Task RunOnceAsync(CancellationToken token)
    {
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
            catch (Exception exception) { logger.LogError(exception, "Contest closure failed for {ContestId}", contestId); }
        }
    }
}
