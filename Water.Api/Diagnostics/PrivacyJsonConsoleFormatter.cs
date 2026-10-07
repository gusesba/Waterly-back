using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Water.Api.Diagnostics;

public sealed class PrivacyJsonConsoleFormatter() : ConsoleFormatter(FormatterName)
{
    public const string FormatterName = "waterly-json";

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        string? correlationId = null;
        scopeProvider?.ForEachScope((scope, _) =>
        {
            if (scope is IEnumerable<KeyValuePair<string, object>> values)
                correlationId = values.FirstOrDefault(pair => pair.Key == "CorrelationId").Value?.ToString() ?? correlationId;
        }, 0);
        // Request scopes can contain raw invitation tokens. Export only our allowlisted correlation field.
        textWriter.WriteLine(JsonSerializer.Serialize(new
        {
            timestamp = DateTimeOffset.UtcNow,
            level = logEntry.LogLevel.ToString(),
            category = logEntry.Category,
            eventId = logEntry.EventId.Id,
            message = logEntry.Formatter(logEntry.State, logEntry.Exception),
            exception = logEntry.Exception?.ToString(),
            correlationId,
            traceId = Activity.Current?.TraceId.ToString()
        }));
    }
}
