using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Water.Infrastructure.Diagnostics;
using Microsoft.Extensions.Logging.Console;

namespace Water.Api.Diagnostics;

public static class TelemetryExtensions
{
    public static void AddWaterTelemetry(this WebApplicationBuilder builder)
    {
        builder.Logging.AddConsole(options => options.FormatterName = PrivacyJsonConsoleFormatter.FormatterName)
            .AddConsoleFormatter<PrivacyJsonConsoleFormatter, ConsoleFormatterOptions>();
        var telemetry = builder.Services.AddOpenTelemetry().ConfigureResource(resource => resource.AddService("waterly-api"));
        telemetry.WithMetrics(metrics =>
        {
            metrics.AddMeter(WaterTelemetry.Name, "Microsoft.AspNetCore.Hosting", "System.Net.Http", "Microsoft.AspNetCore.RateLimiting");
            metrics.AddView("waterly.http.duration", new ExplicitBucketHistogramConfiguration
            {
                Boundaries = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 15]
            });
            if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
                metrics.AddOtlpExporter();
        });
        telemetry.WithTracing(tracing =>
        {
            tracing.AddSource(WaterTelemetry.Name);
            if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
                tracing.AddOtlpExporter();
        });
    }
}
