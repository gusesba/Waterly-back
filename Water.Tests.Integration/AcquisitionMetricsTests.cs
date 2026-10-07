using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Water.Application.Analytics;
using Water.Application.Hydration;
using Water.Application.Profiles;
using Water.Infrastructure.Persistence;

namespace Water.Tests.Integration;

public sealed class AcquisitionMetricsTests(WaterApiFactory factory) : IClassFixture<WaterApiFactory>
{
    [Fact]
    public async Task Acquisition_counts_real_creations_and_joins_without_counting_retries_or_inventing_legacy_registration()
    {
        using var owner = await AccountAsync("contest-admin@example.com");
        using var member = await AccountAsync("acquisition-member@example.com");
        (await owner.PutAsJsonAsync("/api/v1/profile", new { username = "metrics_owner", displayName = "Metrics owner" })).EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaterDbContext>();
            (await db.Users.SingleAsync(item => item.Email == "acquisition-member@example.com")).RegisteredAt = null;
            await db.SaveChangesAsync();
        }
        var quickAdd = new AddDrinkEntryRequest(Guid.NewGuid(), 100, new DateTimeOffset(2026, 9, 15, 15, 0, 0, TimeSpan.Zero), "UTC", "water", "quick-add");
        (await owner.PostAsJsonAsync("/api/v1/hydration/entries", quickAdd)).EnsureSuccessStatusCode();
        (await owner.PostAsJsonAsync("/api/v1/hydration/entries", quickAdd)).EnsureSuccessStatusCode();
        (await owner.PostAsJsonAsync("/api/v1/hydration/entries", quickAdd with { ClientEntryId = Guid.NewGuid(), InputMethod = "custom" })).EnsureSuccessStatusCode();
        (await owner.PostAsJsonAsync("/api/v1/hydration/entries", quickAdd with { ClientEntryId = Guid.NewGuid(), InputMethod = "unknown" })).EnsureSuccessStatusCode();
        (await member.PostAsJsonAsync("/api/v1/hydration/entries", quickAdd with { ClientEntryId = Guid.NewGuid(), InputMethod = "unknown" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/v1/hydration/entries", quickAdd with { InputMethod = "private-payload" })).StatusCode);

        var create = await owner.PostAsJsonAsync("/api/v1/groups", new { name = "Metrics group", description = (string?)null });
        create.EnsureSuccessStatusCode();
        var group = await create.Content.ReadFromJsonAsync<JsonElement>();
        var inviteResponse = await owner.PostAsync($"/api/v1/groups/{group.GetProperty("id").GetString()}/invite", null);
        inviteResponse.EnsureSuccessStatusCode();
        var invite = await inviteResponse.Content.ReadFromJsonAsync<JsonElement>();
        var path = $"/api/v1/invites/group/{invite.GetProperty("token").GetString()}";
        using var anonymous = factory.CreateClient();
        (await anonymous.GetAsync(path)).EnsureSuccessStatusCode();
        (await anonymous.GetAsync(path)).EnsureSuccessStatusCode();
        (await member.PostAsync(path + "/accept", null)).EnsureSuccessStatusCode();
        (await member.PostAsync(path + "/accept", null)).EnsureSuccessStatusCode();

        var report = await owner.GetFromJsonAsync<ProductMetricsResponse>("/api/v1/admin/metrics");
        Assert.NotNull(report);
        Assert.Equal(1, report.Acquisition.FirstRecordAccounts);
        Assert.True(report.Acquisition.AverageTimeToFirstRecordSeconds >= 0);
        Assert.Equal(1, report.Acquisition.QuickAddRecords);
        Assert.Equal(1, report.Acquisition.CustomRecords);
        Assert.Equal(2, report.Acquisition.UnknownInputRecords);
        Assert.Equal(2m, report.Acquisition.RecordsPerActiveUserDay);
        Assert.Equal(1, report.Acquisition.CreatedInvites);
        Assert.Equal(2, report.Acquisition.InvitePreviews);
        Assert.Equal(1, report.Acquisition.InviteAcceptances);
        var export = await owner.GetStringAsync("/api/v1/me/export");
        using var document = JsonDocument.Parse(export);
        Assert.Contains(document.RootElement.GetProperty("hydration").GetProperty("entries").EnumerateArray(),
            item => item.GetProperty("inputMethod").GetString() == "quick-add");
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("account").GetProperty("registeredAt").ValueKind);
        var json = await owner.GetStringAsync("/api/v1/admin/metrics");
        Assert.DoesNotContain("@example.com", json);
        Assert.DoesNotContain(invite.GetProperty("token").GetString()!, json);
    }

    private async Task<HttpClient> AccountAsync(string email)
    {
        var client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "waterly123" })).EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login?useCookies=false", new { email, password = "waterly123" });
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        (await client.PutAsJsonAsync("/api/v1/me/onboarding", new CompleteOnboardingRequest(28, 178, 74.5m, 2000, ["habit"], "UTC"))).EnsureSuccessStatusCode();
        return client;
    }
}
