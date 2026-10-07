using Water.Application.Analytics;
using Water.Api.Features.Competition;

namespace Water.Api.Features.Analytics;

public static class ProductMetricsEndpoints
{
    public static void MapProductMetricsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/admin/metrics", (IProductMetricsService service, CancellationToken token) => service.GetAsync(token))
            .RequireAuthorization(ContestEndpoints.AdminPolicy)
            .RequireRateLimiting("contest-read")
            .WithTags("Administration");
    }
}
