using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Water.Application.Profiles;
using Water.Application.Hydration;
using Water.Application.Habits;
using Water.Application.Progression;
using Water.Domain.Hydration;
using Water.Infrastructure.Identity;
using Water.Infrastructure.Persistence;

namespace Water.Tests.Integration;

public sealed class AccountFlowTests(WaterApiFactory factory) : IClassFixture<WaterApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    [Fact]
    public async Task Account_can_register_login_and_complete_onboarding_idempotently()
    {
        var email = $"person-{Guid.NewGuid():N}@example.com";
        const string password = "waterly123";

        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password
        });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var tokens = await LoginAsync(email, password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = tokens.RefreshToken
        });
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var beforeResponse = await _client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, beforeResponse.StatusCode);
        var before = await beforeResponse.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(before);
        Assert.False(before.HasCompletedOnboarding);

        var request = new CompleteOnboardingRequest(
            28,
            178,
            74.5m,
            2600,
            ["habit", "energy"],
            "America/Sao_Paulo");

        var firstResponse = await _client.PutAsJsonAsync("/api/v1/me/onboarding", request);
        var secondResponse = await _client.PutAsJsonAsync("/api/v1/me/onboarding", request);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var current = await secondResponse.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(current);
        Assert.True(current.HasCompletedOnboarding);
        Assert.Equal(2600, current.HydrationGoal?.DailyTargetMl);
        Assert.Equal("America/Sao_Paulo", current.HydrationGoal?.TimeZone);
        Assert.Equal(["energy", "habit"], current.Profile?.Goals);

        var entryRequest = new AddDrinkEntryRequest(
            Guid.NewGuid(),
            350,
            DateTimeOffset.UtcNow,
            "America/Sao_Paulo");
        var firstEntryResponse = await _client.PostAsJsonAsync(
            "/api/v1/hydration/entries",
            entryRequest);
        var duplicateEntryResponse = await _client.PostAsJsonAsync(
            "/api/v1/hydration/entries",
            entryRequest);

        Assert.Equal(HttpStatusCode.OK, firstEntryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicateEntryResponse.StatusCode);
        var hydration = await duplicateEntryResponse.Content
            .ReadFromJsonAsync<TodayHydrationResponse>();
        Assert.NotNull(hydration);
        Assert.Equal(350, hydration.ConsumedMl);
        Assert.Equal(2600, hydration.DailyTargetMl);
        Assert.Single(hydration.Entries);
        Assert.Equal(entryRequest.ClientEntryId, hydration.Entries.Single().ClientEntryId);
        Assert.Equal("water", hydration.Entries.Single().BeverageCode);

        var beveragesResponse = await _client.GetAsync("/api/v1/hydration/beverages");
        Assert.Equal(HttpStatusCode.OK, beveragesResponse.StatusCode);
        var beverages = await beveragesResponse.Content.ReadFromJsonAsync<BeverageResponse[]>();
        Assert.NotNull(beverages);
        Assert.Equal(4, beverages.Length);
        Assert.Equal(0.800m, beverages.Single(item => item.Code == "coffee").HydrationFactor);

        var waterSuggestionsResponse = await _client.GetAsync(
            "/api/v1/hydration/suggestions?beverageCode=water");
        Assert.Equal(HttpStatusCode.OK, waterSuggestionsResponse.StatusCode);
        var waterSuggestions = await waterSuggestionsResponse.Content
            .ReadFromJsonAsync<QuickAddSuggestionResponse[]>();
        Assert.NotNull(waterSuggestions);
        Assert.Equal(3, waterSuggestions.Length);
        Assert.All(waterSuggestions, item => Assert.Equal("water", item.BeverageCode));

        var entryId = hydration.Entries.Single().Id;
        var updateOperationId = Guid.NewGuid();
        var updateResponse = await _client.PatchAsJsonAsync(
            $"/api/v1/hydration/entries/{entryId}",
            new UpdateDrinkEntryRequest(500, "coffee", updateOperationId));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updatedHydration = await updateResponse.Content.ReadFromJsonAsync<TodayHydrationResponse>();
        Assert.NotNull(updatedHydration);
        Assert.Equal(400, updatedHydration.ConsumedMl);
        Assert.Equal("coffee", updatedHydration.Entries.Single().BeverageCode);
        Assert.Equal(400, updatedHydration.Entries.Single().HydrationMl);
        var duplicateUpdateResponse = await _client.PatchAsJsonAsync(
            $"/api/v1/hydration/entries/{entryId}",
            new UpdateDrinkEntryRequest(500, "coffee", updateOperationId));
        Assert.Equal(HttpStatusCode.OK, duplicateUpdateResponse.StatusCode);

        var suggestionsResponse = await _client.GetAsync(
            "/api/v1/hydration/suggestions?beverageCode=coffee");
        Assert.Equal(HttpStatusCode.OK, suggestionsResponse.StatusCode);
        var suggestions = await suggestionsResponse.Content
            .ReadFromJsonAsync<QuickAddSuggestionResponse[]>();
        Assert.NotNull(suggestions);
        Assert.Equal(new QuickAddSuggestionResponse("coffee", 500), suggestions.First());
        Assert.Equal(3, suggestions.Length);
        Assert.All(suggestions, item => Assert.Equal("coffee", item.BeverageCode));

        var historyResponse = await _client.GetAsync("/api/v1/hydration/history?days=7");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = await historyResponse.Content.ReadFromJsonAsync<HydrationHistoryResponse>();
        Assert.NotNull(history);
        Assert.NotEmpty(history.Days);
        Assert.Equal(400, history.Days.First().ConsumedMl);

        var deleteOperationId = Guid.NewGuid();
        var deletePath = $"/api/v1/hydration/entries/{entryId}?clientOperationId={deleteOperationId}";
        var deleteResponse = await _client.DeleteAsync(deletePath);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        var emptyHydration = await deleteResponse.Content.ReadFromJsonAsync<TodayHydrationResponse>();
        Assert.NotNull(emptyHydration);
        Assert.Equal(0, emptyHydration.ConsumedMl);
        Assert.Empty(emptyHydration.Entries);
        var duplicateDeleteResponse = await _client.DeleteAsync(deletePath);
        Assert.Equal(HttpStatusCode.OK, duplicateDeleteResponse.StatusCode);
    }

    [Fact]
    public async Task Profile_requires_authentication()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Hydration_requires_authentication()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/hydration/today");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Streak_rebuilds_after_hydration_changes()
    {
        var email = $"streak-{Guid.NewGuid():N}@example.com";
        const string password = "waterly123";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        var tokens = await LoginAsync(email, password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        await _client.PutAsJsonAsync("/api/v1/me/onboarding", new CompleteOnboardingRequest(
            28, 178, 74.5m, 500, ["habit"], "UTC"));

        var yesterday = new DateTimeOffset(
            DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-1).AddHours(12), DateTimeKind.Utc));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);
            var dbContext = scope.ServiceProvider.GetRequiredService<WaterDbContext>();
            dbContext.HydrationGoals.Add(new HydrationGoal(
                user.Id, 500, DateOnly.FromDateTime(yesterday.UtcDateTime)));
            await dbContext.SaveChangesAsync();
        }
        var yesterdayResponse = await _client.PostAsJsonAsync("/api/v1/hydration/entries", new AddDrinkEntryRequest(
            Guid.NewGuid(), 500, yesterday, "UTC"));
        Assert.Equal(HttpStatusCode.OK, yesterdayResponse.StatusCode);
        var yesterdayStreak = await _client.GetFromJsonAsync<StreakResponse>("/api/v1/habits/streak");
        Assert.NotNull(yesterdayStreak);
        Assert.Equal(1, yesterdayStreak.Current);
        Assert.False(yesterdayStreak.TodayCompleted);

        var todayEntry = new AddDrinkEntryRequest(Guid.NewGuid(), 500, DateTimeOffset.UtcNow, "UTC");
        var hydrationResponse = await _client.PostAsJsonAsync("/api/v1/hydration/entries", todayEntry);
        var hydration = await hydrationResponse.Content.ReadFromJsonAsync<TodayHydrationResponse>();
        Assert.NotNull(hydration);
        var completedStreak = await _client.GetFromJsonAsync<StreakResponse>("/api/v1/habits/streak");
        Assert.NotNull(completedStreak);
        Assert.Equal(2, completedStreak.Current);
        Assert.Equal(2, completedStreak.Longest);
        Assert.True(completedStreak.TodayCompleted);

        var deletePath = $"/api/v1/hydration/entries/{hydration.Entries.Single().Id}?clientOperationId={Guid.NewGuid()}";
        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync(deletePath)).StatusCode);
        var recalculated = await _client.GetFromJsonAsync<StreakResponse>("/api/v1/habits/streak");
        Assert.NotNull(recalculated);
        Assert.Equal(1, recalculated.Current);
        Assert.False(recalculated.TodayCompleted);
    }

    [Fact]
    public async Task Achievement_is_unlocked_once_and_is_not_revoked()
    {
        var email = $"achievement-{Guid.NewGuid():N}@example.com";
        const string password = "waterly123";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        var tokens = await LoginAsync(email, password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        await _client.PutAsJsonAsync("/api/v1/me/onboarding", new CompleteOnboardingRequest(
            28, 178, 74.5m, 500, ["habit"], "UTC"));

        var entryResponse = await _client.PostAsJsonAsync(
            "/api/v1/hydration/entries",
            new AddDrinkEntryRequest(Guid.NewGuid(), 500, DateTimeOffset.UtcNow, "UTC"));
        var hydration = await entryResponse.Content.ReadFromJsonAsync<TodayHydrationResponse>();
        Assert.NotNull(hydration);

        var firstRead = await _client.GetFromJsonAsync<AchievementResponse[]>("/api/v1/achievements");
        var secondRead = await _client.GetFromJsonAsync<AchievementResponse[]>("/api/v1/achievements");
        Assert.NotNull(firstRead);
        Assert.NotNull(secondRead);
        var firstGoal = firstRead.Single(item => item.Code == "first-goal");
        Assert.True(firstGoal.IsUnlocked);
        Assert.Equal(firstGoal.UnlockedAt, secondRead.Single(item => item.Code == "first-goal").UnlockedAt);
        var drops = await _client.GetFromJsonAsync<ProgressionBalanceResponse>("/api/v1/wallet");
        var prestige = await _client.GetFromJsonAsync<ProgressionBalanceResponse>("/api/v1/prestige");
        Assert.NotNull(drops);
        Assert.NotNull(prestige);
        Assert.Equal(25, drops.Balance);
        Assert.Equal(10, prestige.Balance);
        Assert.Single(drops.Entries);
        Assert.Single(prestige.Entries);

        var deletePath = $"/api/v1/hydration/entries/{hydration.Entries.Single().Id}?clientOperationId={Guid.NewGuid()}";
        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync(deletePath)).StatusCode);
        var afterDelete = await _client.GetFromJsonAsync<AchievementResponse[]>("/api/v1/achievements");
        Assert.NotNull(afterDelete);
        Assert.True(afterDelete.Single(item => item.Code == "first-goal").IsUnlocked);
        Assert.Equal(25, (await _client.GetFromJsonAsync<ProgressionBalanceResponse>("/api/v1/wallet"))?.Balance);
        Assert.Equal(10, (await _client.GetFromJsonAsync<ProgressionBalanceResponse>("/api/v1/prestige"))?.Balance);
    }

    [Fact]
    public async Task Achievements_require_authentication()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/achievements");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/wallet")]
    [InlineData("/api/v1/prestige")]
    public async Task Progression_requires_authentication(string path)
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Public_profile_can_be_created_without_exposing_physical_data()
    {
        var email = $"profile-{Guid.NewGuid():N}@example.com"; const string password = "waterly123";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        var tokens = await LoginAsync(email, password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var response = await _client.PutAsJsonAsync("/api/v1/profile", new UpdatePublicProfileRequest("water_user", "Water User", "Olá"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("weight", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("height", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("age", json, StringComparison.OrdinalIgnoreCase);
        var profile = JsonSerializer.Deserialize<PublicProfileResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("water_user", profile?.Username);
    }

    [Fact]
    public async Task Public_profile_requires_authentication()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/profile")).StatusCode);
    }

    [Fact]
    public async Task Streak_requires_authentication()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/habits/streak");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<(string AccessToken, string RefreshToken)> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login?useCookies=false", new
        {
            email,
            password
        });
        response.EnsureSuccessStatusCode();

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var accessToken = body.RootElement.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("Login response has no access token.");
        var refreshToken = body.RootElement.GetProperty("refreshToken").GetString()
            ?? throw new InvalidOperationException("Login response has no refresh token.");

        return (accessToken, refreshToken);
    }
}
