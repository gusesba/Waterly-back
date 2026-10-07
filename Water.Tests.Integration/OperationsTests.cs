using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Water.Api.Diagnostics;
using Water.Application.Analytics;
using Water.Domain.Habits;
using Water.Domain.Social;
using Water.Infrastructure.Identity;
using Water.Infrastructure.Persistence;

namespace Water.Tests.Integration;

public sealed class OperationsTests(WaterApiFactory factory) : IClassFixture<WaterApiFactory>
{
    [Fact]
    public void Structured_logs_export_correlation_without_private_request_scopes()
    {
        var scopes = new LoggerExternalScopeProvider();
        using var requestScope = scopes.Push(new Dictionary<string, object>
        {
            ["RequestPath"] = "/api/v1/invites/private-invite-token",
            ["Authorization"] = "Bearer private-access-token"
        });
        using var correlationScope = scopes.Push(new Dictionary<string, object> { ["CorrelationId"] = "safe-correlation" });
        var entry = new LogEntry<string>(LogLevel.Information, "Waterly", new EventId(1), "job completed", null, (state, _) => state);
        using var output = new StringWriter();
        new PrivacyJsonConsoleFormatter().Write(entry, scopes, output);
        var json = output.ToString();
        Assert.DoesNotContain("private-invite-token", json);
        Assert.DoesNotContain("private-access-token", json);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("safe-correlation", document.RootElement.GetProperty("correlationId").GetString());
        Assert.Equal("job completed", document.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Correlation_is_returned_and_unsafe_values_are_replaced()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "test-correlation-123");
        var response = await client.GetAsync("/health/live");
        Assert.Equal("test-correlation-123", response.Headers.GetValues("X-Correlation-ID").Single());
        client.DefaultRequestHeaders.Remove("X-Correlation-ID");
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "secret@example.com");
        response = await client.GetAsync("/api/v1/groups");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var returned = response.Headers.GetValues("X-Correlation-ID").Single();
        Assert.Equal(32, returned.Length);
        Assert.DoesNotContain("secret", returned);
    }

    [Fact]
    public async Task Product_metrics_require_admin_and_report_eligible_cohorts_without_private_data()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/admin/metrics")).StatusCode);
        var email = "contest-admin@example.com";
        (await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "waterly123" })).EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login?useCookies=false", new { email, password = "waterly123" });
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaterDbContext>();
            var first = new DateOnly(2026, 8, 16);
            var grouped = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "grouped-private", Email = "grouped-secret@example.com" };
            var solo = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "solo-private" };
            var recent = new ApplicationUser { Id = Guid.NewGuid().ToString(), UserName = "recent-private" };
            db.Users.AddRange(grouped, solo, recent);
            var group = new PrivateGroup(grouped.Id, "Private secret group", null, DateTimeOffset.UtcNow);
            db.PrivateGroups.Add(group);
            db.GroupMemberships.Add(new GroupMembership(group.Id, grouped.Id, "owner", DateTimeOffset.UtcNow));
            foreach (var day in new[] { 0, 1, 7, 30 })
                db.DailyHydrations.Add(new DailyHydration(grouped.Id, first.AddDays(day), "UTC", 2000, 1000, null));
            db.DailyHydrations.Add(new DailyHydration(solo.Id, first, "UTC", 2000, 1000, null));
            db.DailyHydrations.Add(new DailyHydration(recent.Id, new DateOnly(2026, 9, 15), "UTC", 2000, 1000, null));
            await db.SaveChangesAsync();
        }

        var report = await client.GetFromJsonAsync<ProductMetricsResponse>("/api/v1/admin/metrics");
        Assert.NotNull(report);
        Assert.Equal(6, report.Retention.Count);
        Assert.All(report.Retention.Where(row => row.Segment == "with-group"), row =>
        { Assert.Equal(1, row.EligibleUsers); Assert.Equal(1, row.RetainedUsers); Assert.Equal(1m, row.Rate); });
        Assert.All(report.Retention.Where(row => row.Segment == "without-group"), row =>
        { Assert.Equal(1, row.EligibleUsers); Assert.Equal(0, row.RetainedUsers); Assert.Equal(0m, row.Rate); });
        var json = await client.GetStringAsync("/api/v1/admin/metrics");
        foreach (var secret in new[] { "@example.com", "userId", "hydrationMl", "weightKg", "Private secret group" })
            Assert.DoesNotContain(secret, json);

        (await client.PostAsJsonAsync("/api/v1/auth/register", new { email = "regular-metrics@example.com", password = "waterly123" })).EnsureSuccessStatusCode();
        login = await client.PostAsJsonAsync("/api/v1/auth/login?useCookies=false", new { email = "regular-metrics@example.com", password = "waterly123" });
        session = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/metrics")).StatusCode);
    }
}
