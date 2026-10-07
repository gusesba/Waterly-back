using System.Diagnostics;
using Microsoft.AspNetCore.Routing;
using Water.Infrastructure.Diagnostics;

namespace Water.Api.Diagnostics;

public sealed class RequestTelemetryMiddleware(RequestDelegate next, ILogger<RequestTelemetryMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers["X-Correlation-ID"].ToString();
        var correlationId = supplied.Length is > 0 and <= 64 && supplied.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
            ? supplied : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = correlationId;
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        using var activity = WaterTelemetry.Activities.StartActivity("http.request", ActivityKind.Internal);
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });
        var started = Stopwatch.GetTimestamp();
        var status = 500;
        try
        {
            await next(context);
            status = context.Response.StatusCode;
        }
        finally
        {
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
            var tags = new TagList { { "http.request.method", context.Request.Method }, { "http.route", route }, { "http.response.status_code", status } };
            WaterTelemetry.RequestDuration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, tags);
            activity?.SetTag("http.route", route);
            activity?.SetTag("http.request.method", context.Request.Method);
            activity?.SetTag("http.response.status_code", status);
            if (status >= 500) activity?.SetStatus(ActivityStatusCode.Error);
        }
    }
}
