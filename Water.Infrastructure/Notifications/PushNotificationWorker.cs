using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Water.Infrastructure.Diagnostics;

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
        var started = Stopwatch.GetTimestamp();
        using var activity = WaterTelemetry.Activities.StartActivity("job.push-notifications");
        try
        {
            using var scope = scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<PushNotificationProcessor>().ProcessAsync(token);
            WaterTelemetry.RecordJob("push-notifications", true, Stopwatch.GetElapsedTime(started));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            WaterTelemetry.RecordJob("push-notifications", false, Stopwatch.GetElapsedTime(started));
            activity?.SetStatus(ActivityStatusCode.Error);
            logger.LogError(exception, "Push notification batch failed and will be retried");
        }
    }
}
