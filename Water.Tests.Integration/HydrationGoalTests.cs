using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Water.Application.Hydration;
using Water.Application.Profiles;

namespace Water.Tests.Integration;

public sealed class HydrationGoalTests(GoalApiFactory factory) : IClassFixture<GoalApiFactory>
{
    [Fact]
    public async Task Goal_changes_start_on_the_next_local_day_and_preserve_today_and_history()
    {
        using var client = factory.CreateClient();
        var email = $"goal-{Guid.NewGuid():N}@example.com";
        (await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "waterly123" })).EnsureSuccessStatusCode();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login?useCookies=false", new { email, password = "waterly123" });
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync("/api/v1/hydration/goal")).StatusCode);
        (await client.PutAsJsonAsync("/api/v1/me/onboarding", new CompleteOnboardingRequest(28, 178, 74.5m, 2000, ["habit"], "America/Sao_Paulo"))).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/v1/hydration/entries", new AddDrinkEntryRequest(Guid.NewGuid(), 500, factory.Clock.GetUtcNow(), "America/Sao_Paulo"))).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync("/api/v1/me/onboarding", new CompleteOnboardingRequest(28, 178, 74.5m, 2800, ["habit"], "America/Sao_Paulo"))).EnsureSuccessStatusCode();
        Assert.Equal(2000, (await client.GetFromJsonAsync<CurrentUserResponse>("/api/v1/me"))!.HydrationGoal!.DailyTargetMl);
        Assert.Equal(2800, (await client.GetFromJsonAsync<HydrationGoalSettingsResponse>("/api/v1/hydration/goal"))!.ScheduledTargetMl);
        foreach (var amount in new[] { 3000, 2500, 2500 })
            (await client.PutAsJsonAsync("/api/v1/hydration/goal", new UpdateHydrationGoalRequest(amount))).EnsureSuccessStatusCode();
        var goal = await client.GetFromJsonAsync<HydrationGoalSettingsResponse>("/api/v1/hydration/goal");
        Assert.Equal(2000, goal!.DailyTargetMl);
        Assert.Equal(2500, goal.ScheduledTargetMl);
        Assert.Equal(new DateOnly(2026, 9, 16), goal.EffectiveFrom);
        Assert.Equal(2000, (await client.GetFromJsonAsync<CurrentUserResponse>("/api/v1/me"))!.HydrationGoal!.DailyTargetMl);
        var today = await client.GetFromJsonAsync<TodayHydrationResponse>("/api/v1/hydration/today");
        Assert.Equal(2000, today!.DailyTargetMl);
        Assert.Equal(0.25, today.Progress);
        foreach (var amount in new[] { 499, 6001 })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/v1/hydration/goal", new { dailyTargetMl = amount })).StatusCode);

        factory.Clock.UtcNow = new DateTimeOffset(2026, 9, 16, 15, 0, 0, TimeSpan.Zero);
        login = await client.PostAsJsonAsync("/api/v1/auth/login?useCookies=false", new { email, password = "waterly123" });
        session = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        today = await client.GetFromJsonAsync<TodayHydrationResponse>("/api/v1/hydration/today");
        Assert.Equal(2500, today!.DailyTargetMl);
        var history = await client.GetFromJsonAsync<HydrationHistoryResponse>("/api/v1/hydration/history?days=2");
        Assert.Equal(2000, history!.Days.Single(day => day.Date == new DateOnly(2026, 9, 15)).DailyTargetMl);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/hydration/goal")).StatusCode);
    }
}

public sealed class GoalApiFactory : WaterApiFactory
{
    public MutableTimeProvider Clock { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(Clock); });
    }
}

public sealed class MutableTimeProvider : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 9, 15, 15, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => UtcNow;
}
