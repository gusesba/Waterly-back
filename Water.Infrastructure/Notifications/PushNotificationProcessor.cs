using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Water.Domain.Notifications;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Notifications;

public sealed class PushNotificationProcessor(
    WaterDbContext dbContext,
    IExpoPushGateway gateway,
    IOptions<PushNotificationOptions> options,
    TimeProvider timeProvider)
{
    public async Task ProcessAsync(CancellationToken token)
    {
        await SendPendingAsync(token);
        await CheckReceiptsAsync(token);
    }

    private async Task SendPendingAsync(CancellationToken token)
    {
        var now = timeProvider.GetUtcNow();
        var candidates = await dbContext.PushNotificationMessages
            .Include(item => item.DeviceInstallation)
            .Where(item => item.AcceptedAt == null && item.CompletedAt == null)
            .ToArrayAsync(token);
        var pending = candidates.Where(item => item.NextAttemptAt <= now && item.DeviceInstallation.DisabledAt == null)
            .OrderBy(item => item.CreatedAt)
            .Take(options.Value.BatchSize)
            .ToArray();
        if (pending.Length == 0) return;

        try
        {
            var envelopes = pending.Select(item => new ExpoPushEnvelope(
                item.DeviceInstallation.ExpoPushToken,
                item.Title,
                item.Body,
                JsonSerializer.Deserialize<JsonElement>(item.DataJson))).ToArray();
            var results = (await gateway.SendAsync(envelopes, token)).ToArray();
            if (results.Length != pending.Length) throw new InvalidOperationException("Expo returned a different number of push tickets.");

            for (var index = 0; index < pending.Length; index++) ApplyTicket(pending[index], results[index], now);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            foreach (var message in pending) RetryOrComplete(message, exception.GetType().Name, now);
        }
        await dbContext.SaveChangesAsync(token);
    }

    private async Task CheckReceiptsAsync(CancellationToken token)
    {
        var now = timeProvider.GetUtcNow();
        var dueAt = now - options.Value.ReceiptDelay;
        var accepted = (await dbContext.PushNotificationMessages
                .Include(item => item.DeviceInstallation)
                .Where(item => item.TicketId != null && item.CompletedAt == null)
                .ToArrayAsync(token))
            .Where(item => item.AcceptedAt <= dueAt)
            .OrderBy(item => item.AcceptedAt)
            .Take(options.Value.BatchSize)
            .ToArray();
        if (accepted.Length == 0) return;

        try
        {
            var receipts = await gateway.GetReceiptsAsync(accepted.Select(item => item.TicketId!).ToArray(), token);
            foreach (var message in accepted)
            {
                if (!receipts.TryGetValue(message.TicketId!, out var receipt))
                {
                    RetryOrComplete(message, "receipt-missing", now);
                    continue;
                }
                if (receipt.Status == "ok") message.Complete(now);
                else if (receipt.Error == "DeviceNotRegistered")
                {
                    message.DeviceInstallation.Disable(now);
                    message.Complete(now, receipt.Error);
                }
                else if (receipt.Error is "MessageRateExceeded") RetryOrComplete(message, receipt.Error, now);
                else message.Complete(now, receipt.Error ?? receipt.Message ?? "receipt-error");
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch
        {
            return;
        }
        await dbContext.SaveChangesAsync(token);
    }

    private void ApplyTicket(PushNotificationMessage message, ExpoPushResult result, DateTimeOffset now)
    {
        if (result.Status == "ok" && result.TicketId is not null) message.Accept(result.TicketId, now);
        else if (result.Error == "DeviceNotRegistered")
        {
            message.DeviceInstallation.Disable(now);
            message.Complete(now, result.Error);
        }
        else if (result.Error is "MessageRateExceeded") RetryOrComplete(message, result.Error, now);
        else message.Complete(now, result.Error ?? result.Message ?? "ticket-error");
    }

    private void RetryOrComplete(PushNotificationMessage message, string error, DateTimeOffset now)
    {
        if (message.AttemptCount + 1 >= options.Value.MaxAttempts) message.Complete(now, error);
        else message.Retry(error, now + TimeSpan.FromSeconds(15 * Math.Pow(2, message.AttemptCount)));
    }
}
