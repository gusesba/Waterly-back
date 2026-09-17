using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Water.Infrastructure.Notifications;

public sealed record ExpoPushEnvelope(string To, string Title, string Body, JsonElement Data);
public sealed record ExpoPushResult(string Status, string? TicketId, string? Error, string? Message);

public interface IExpoPushGateway
{
    Task<IReadOnlyCollection<ExpoPushResult>> SendAsync(IReadOnlyCollection<ExpoPushEnvelope> messages, CancellationToken token);
    Task<IReadOnlyDictionary<string, ExpoPushResult>> GetReceiptsAsync(IReadOnlyCollection<string> ticketIds, CancellationToken token);
}

public sealed class ExpoPushGateway(HttpClient client, IOptions<PushNotificationOptions> options) : IExpoPushGateway
{
    public async Task<IReadOnlyCollection<ExpoPushResult>> SendAsync(IReadOnlyCollection<ExpoPushEnvelope> messages, CancellationToken token)
    {
        using var request = Request(HttpMethod.Post, "--/api/v2/push/send");
        request.Content = JsonContent.Create(messages.Select(item => new { to = item.To, title = item.Title, body = item.Body, sound = "default", data = item.Data }).ToArray());
        using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(token));
        return document.RootElement.GetProperty("data").EnumerateArray().Select(Parse).ToArray();
    }

    public async Task<IReadOnlyDictionary<string, ExpoPushResult>> GetReceiptsAsync(IReadOnlyCollection<string> ticketIds, CancellationToken token)
    {
        using var request = Request(HttpMethod.Post, "--/api/v2/push/getReceipts");
        request.Content = JsonContent.Create(new { ids = ticketIds });
        using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(token));
        return document.RootElement.GetProperty("data").EnumerateObject().ToDictionary(item => item.Name, item => Parse(item.Value));
    }

    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        if (!string.IsNullOrWhiteSpace(options.Value.ExpoAccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ExpoAccessToken);
        return request;
    }

    private static ExpoPushResult Parse(JsonElement item)
    {
        var status = item.GetProperty("status").GetString() ?? "error";
        var ticketId = item.TryGetProperty("id", out var id) ? id.GetString() : null;
        var message = item.TryGetProperty("message", out var messageValue) ? messageValue.GetString() : null;
        string? error = null;
        if (item.TryGetProperty("details", out var details) && details.TryGetProperty("error", out var errorValue)) error = errorValue.GetString();
        return new ExpoPushResult(status, ticketId, error, message);
    }
}
