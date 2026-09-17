using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Water.Infrastructure.Notifications;

public sealed class PushNotificationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<PushNotificationOptions> options,
    ILogger<PushNotificationWorker> logger) : BackgroundService
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
        try
        {
            using var scope = scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<PushNotificationProcessor>().ProcessAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception) { logger.LogError(exception, "Push notification batch failed and will be retried"); }
    }
}
