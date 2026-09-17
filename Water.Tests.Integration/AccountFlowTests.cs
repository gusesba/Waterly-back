using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Water.Application.Profiles;
using Water.Application.Hydration;
using Water.Application.Habits;
using Water.Application.Progression;
using Water.Domain.Hydration;
using Water.Application.Cosmetics;
using Water.Application.Social;
using Water.Application.Feed;
using Water.Domain.Feed;
using Water.Application.Competition;
using Water.Domain.Competition;
using Water.Infrastructure.Identity;
using Water.Infrastructure.Persistence;
using Water.Application.Notifications;
using Water.Domain.Notifications;
using Water.Infrastructure.Notifications;
using Microsoft.Extensions.Options;

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
            new DateTimeOffset(2026, 9, 15, 14, 0, 0, TimeSpan.Zero),
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
            new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc));
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

        var todayEntry = new AddDrinkEntryRequest(Guid.NewGuid(), 500, new DateTimeOffset(2026, 9, 15, 14, 0, 0, TimeSpan.Zero), "UTC");
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
            new AddDrinkEntryRequest(Guid.NewGuid(), 500, new DateTimeOffset(2026, 9, 15, 14, 0, 0, TimeSpan.Zero), "UTC"));
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
        var feed = await _client.GetFromJsonAsync<FeedPageResponse>("/api/v1/feed");
        Assert.Single(feed!.Items, item => item.Type == "achievement-unlocked" && item.ReferenceId == "first-goal");

        var deletePath = $"/api/v1/hydration/entries/{hydration.Entries.Single().Id}?clientOperationId={Guid.NewGuid()}";
        Assert.Equal(HttpStatusCode.OK, (await _client.DeleteAsync(deletePath)).StatusCode);
        var afterDelete = await _client.GetFromJsonAsync<AchievementResponse[]>("/api/v1/achievements");
        Assert.NotNull(afterDelete);
        Assert.True(afterDelete.Single(item => item.Code == "first-goal").IsUnlocked);
        Assert.Equal(25, (await _client.GetFromJsonAsync<ProgressionBalanceResponse>("/api/v1/wallet"))?.Balance);
        Assert.Equal(10, (await _client.GetFromJsonAsync<ProgressionBalanceResponse>("/api/v1/prestige"))?.Balance);
        Assert.Single((await _client.GetFromJsonAsync<FeedPageResponse>("/api/v1/feed"))!.Items, item => item.Type == "achievement-unlocked" && item.ReferenceId == "first-goal");
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
    public async Task Achievement_unlocks_permanent_aura_that_can_be_equipped_without_spending_currency()
    {
        var email = $"cosmetics-{Guid.NewGuid():N}@example.com";
        const string password = "waterly123";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        var tokens = await LoginAsync(email, password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        await _client.PutAsJsonAsync("/api/v1/me/onboarding", new CompleteOnboardingRequest(
            28, 178, 74.5m, 500, ["habit"], "UTC"));

        var initial = await _client.GetFromJsonAsync<CharacterLoadoutResponse>("/api/v1/profile/loadout");
        Assert.NotNull(initial);
        Assert.Equal("axolotl-pink", initial.CharacterCode);
        Assert.Equal("natural", initial.AuraCode);
        Assert.True(initial.Auras.Single(item => item.Code == "natural").IsOwned);
        Assert.False(initial.Auras.Single(item => item.Code == "ocean").IsOwned);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PutAsJsonAsync(
            "/api/v1/profile/loadout", new UpdateCharacterLoadoutRequest("ocean"))).StatusCode);

        await _client.PostAsJsonAsync("/api/v1/hydration/entries",
            new AddDrinkEntryRequest(Guid.NewGuid(), 500, new DateTimeOffset(2026, 9, 15, 14, 0, 0, TimeSpan.Zero), "UTC"));
        var unlocked = await _client.GetFromJsonAsync<CharacterLoadoutResponse>("/api/v1/cosmetics");
        Assert.NotNull(unlocked);
        Assert.True(unlocked.Auras.Single(item => item.Code == "ocean").IsOwned);
        var dropsBeforeEquip = await _client.GetFromJsonAsync<ProgressionBalanceResponse>("/api/v1/wallet");
        var prestigeBeforeEquip = await _client.GetFromJsonAsync<ProgressionBalanceResponse>("/api/v1/prestige");

        var equipResponse = await _client.PutAsJsonAsync(
            "/api/v1/profile/loadout", new UpdateCharacterLoadoutRequest("ocean"));
        Assert.Equal(HttpStatusCode.OK, equipResponse.StatusCode);
        var equipped = await equipResponse.Content.ReadFromJsonAsync<CharacterLoadoutResponse>();
        Assert.Equal("ocean", equipped?.AuraCode);
        Assert.Equal(dropsBeforeEquip?.Balance, (await _client.GetFromJsonAsync<ProgressionBalanceResponse>("/api/v1/wallet"))?.Balance);
        Assert.Equal(prestigeBeforeEquip?.Balance, (await _client.GetFromJsonAsync<ProgressionBalanceResponse>("/api/v1/prestige"))?.Balance);

        var repeated = await _client.GetFromJsonAsync<CharacterLoadoutResponse>("/api/v1/cosmetics");
        Assert.Equal(4, repeated?.Auras.Count);
        Assert.Equal(2, repeated?.Auras.Count(item => item.IsOwned));
    }

    [Theory]
    [InlineData("/api/v1/cosmetics")]
    [InlineData("/api/v1/profile/loadout")]
    public async Task Cosmetics_require_authentication(string path)
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Daily_closure_uses_local_yesterday_and_is_idempotent()
    {
        var email = $"closure-{Guid.NewGuid():N}@example.com";
        const string password = "waterly123";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        var tokens = await LoginAsync(email, password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        await _client.PutAsJsonAsync("/api/v1/me/onboarding", new CompleteOnboardingRequest(
            28, 178, 74.5m, 500, ["habit"], "America/Sao_Paulo"));
        await _client.PostAsJsonAsync("/api/v1/hydration/entries", new AddDrinkEntryRequest(
            Guid.NewGuid(), 500, new DateTimeOffset(2026, 9, 15, 15, 0, 0, TimeSpan.Zero), "America/Sao_Paulo"));

        string userId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<WaterDbContext>();
            userId = await dbContext.Users.Where(item => item.Email == email).Select(item => item.Id).SingleAsync();
            var service = scope.ServiceProvider.GetRequiredService<IDailyClosureService>();
            var candidate = (await service.GetDueAsync(1000, CancellationToken.None))
                .Single(item => item.UserId == userId);
            Assert.Equal(new DateOnly(2026, 9, 14), candidate.CloseThrough);
            await service.CloseAsync(candidate, CancellationToken.None);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IDailyClosureService>();
            await service.CloseAsync(new DailyClosureCandidate(userId, new DateOnly(2026, 9, 14)), CancellationToken.None);
            var dbContext = scope.ServiceProvider.GetRequiredService<WaterDbContext>();
            Assert.Equal(1, await dbContext.DailyClosureCheckpoints.CountAsync(item => item.UserId == userId));
            Assert.Equal(1, await dbContext.UserAchievements.CountAsync(item => item.UserId == userId));
            Assert.Equal(1, await dbContext.DropsLedgerEntries.CountAsync(item => item.UserId == userId));
            Assert.Equal(1, await dbContext.PrestigeLedgerEntries.CountAsync(item => item.UserId == userId));
        }
    }

    [Fact]
    public async Task Streak_requires_authentication()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/habits/streak");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Friend_requests_are_private_authorized_and_idempotent()
    {
        const string password = "waterly123";
        var aliceEmail = $"alice-{Guid.NewGuid():N}@example.com";
        var bobEmail = $"bob-{Guid.NewGuid():N}@example.com";
        var charlieEmail = $"charlie-{Guid.NewGuid():N}@example.com";
        foreach (var email in new[] { aliceEmail, bobEmail, charlieEmail })
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        var alice = await LoginAsync(aliceEmail, password);
        var bob = await LoginAsync(bobEmail, password);
        var charlie = await LoginAsync(charlieEmail, password);

        async Task SetProfile(string token, string username, string name)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _client.PutAsJsonAsync("/api/v1/profile", new UpdatePublicProfileRequest(username, name, "Bio pública"));
            response.EnsureSuccessStatusCode();
        }
        await SetProfile(alice.AccessToken, "alice_social", "Alice");
        await SetProfile(bob.AccessToken, "bob_social", "Bob");
        await SetProfile(charlie.AccessToken, "charlie_social", "Charlie");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", alice.AccessToken);
        var searchJson = await _client.GetStringAsync("/api/v1/profiles/search?query=bob");
        Assert.Contains("bob_social", searchJson);
        Assert.DoesNotContain("email", searchJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("weight", searchJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync(
            "/api/v1/friends/requests", new CreateFriendRequest("alice_social"))).StatusCode);
        var first = await (await _client.PostAsJsonAsync("/api/v1/friends/requests", new CreateFriendRequest("bob_social")))
            .Content.ReadFromJsonAsync<FriendRequestResponse>();
        var repeated = await (await _client.PostAsJsonAsync("/api/v1/friends/requests", new CreateFriendRequest("bob_social")))
            .Content.ReadFromJsonAsync<FriendRequestResponse>();
        Assert.Equal(first?.Id, repeated?.Id);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bob.AccessToken);
        var crossed = await (await _client.PostAsJsonAsync("/api/v1/friends/requests", new CreateFriendRequest("alice_social")))
            .Content.ReadFromJsonAsync<FriendRequestResponse>();
        Assert.Equal("friends", crossed?.Direction);
        Assert.Single((await _client.GetFromJsonAsync<SocialProfileResponse[]>("/api/v1/friends"))!);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", alice.AccessToken);
        var pending = await (await _client.PostAsJsonAsync("/api/v1/friends/requests", new CreateFriendRequest("charlie_social")))
            .Content.ReadFromJsonAsync<FriendRequestResponse>();
        Assert.NotNull(pending);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bob.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsync($"/api/v1/friends/requests/{pending.Id}/accept", null)).StatusCode);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", charlie.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PutAsync($"/api/v1/friends/requests/{pending.Id}/accept", null)).StatusCode);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", alice.AccessToken);
        var bobProfile = (await _client.GetFromJsonAsync<SocialProfileResponse[]>("/api/v1/friends"))!.Single(item => item.Username == "bob_social");
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/v1/friends/{bobProfile.UserId}")).StatusCode);
        Assert.DoesNotContain((await _client.GetFromJsonAsync<SocialProfileResponse[]>("/api/v1/friends"))!, item => item.Username == "bob_social");
    }

    [Theory]
    [InlineData("/api/v1/friends")]
    [InlineData("/api/v1/friends/requests")]
    [InlineData("/api/v1/profiles/search?query=test")]
    public async Task Social_endpoints_require_authentication(string path)
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Blocking_removes_social_contact_hides_activity_and_preserves_shared_groups()
    {
        const string password = "waterly123";
        var aliceEmail = $"block-alice-{Guid.NewGuid():N}@example.com";
        var bobEmail = $"block-bob-{Guid.NewGuid():N}@example.com";
        foreach (var email in new[] { aliceEmail, bobEmail })
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        var alice = await LoginAsync(aliceEmail, password);
        var bob = await LoginAsync(bobEmail, password);

        async Task SaveProfile(string accessToken, string username)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            (await _client.PutAsJsonAsync("/api/v1/profile", new UpdatePublicProfileRequest(username, username, null))).EnsureSuccessStatusCode();
        }
        var aliceUsername = $"ba_{Guid.NewGuid():N}"[..20];
        var bobUsername = $"bb_{Guid.NewGuid():N}"[..20];
        await SaveProfile(alice.AccessToken, aliceUsername);
        await SaveProfile(bob.AccessToken, bobUsername);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", alice.AccessToken);
        var bobProfile = (await _client.GetFromJsonAsync<SocialProfileResponse[]>($"/api/v1/profiles/search?query={bobUsername[..3]}"))!.Single();
        await _client.PostAsJsonAsync("/api/v1/friends/requests", new CreateFriendRequest(bobUsername));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bob.AccessToken);
        (await _client.PostAsJsonAsync("/api/v1/friends/requests", new CreateFriendRequest(aliceUsername))).EnsureSuccessStatusCode();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", alice.AccessToken);
        var group = await (await _client.PostAsJsonAsync("/api/v1/groups", new SaveGroupRequest("Block test", null))).Content.ReadFromJsonAsync<GroupDetailResponse>();
        (await _client.PostAsJsonAsync($"/api/v1/groups/{group!.Id}/members", new AddGroupMemberRequest(bobProfile.UserId))).EnsureSuccessStatusCode();
        Assert.Contains((await _client.GetFromJsonAsync<FeedPageResponse>("/api/v1/feed"))!.Items, item => item.Actor.UserId == bobProfile.UserId);

        string aliceId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaterDbContext>();
            aliceId = (await db.Users.SingleAsync(item => item.Email == aliceEmail)).Id;
        }
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PutAsync($"/api/v1/blocks/{aliceId}", null)).StatusCode);
        (await _client.PutAsync($"/api/v1/blocks/{bobProfile.UserId}", null)).EnsureSuccessStatusCode();
        (await _client.PutAsync($"/api/v1/blocks/{bobProfile.UserId}", null)).EnsureSuccessStatusCode();
        Assert.Single((await _client.GetFromJsonAsync<SocialProfileResponse[]>("/api/v1/blocks"))!, item => item.UserId == bobProfile.UserId);
        Assert.Empty((await _client.GetFromJsonAsync<SocialProfileResponse[]>("/api/v1/friends"))!);
        Assert.Empty((await _client.GetFromJsonAsync<SocialProfileResponse[]>($"/api/v1/profiles/search?query={bobUsername[..3]}"))!);
        Assert.DoesNotContain((await _client.GetFromJsonAsync<FeedPageResponse>("/api/v1/feed"))!.Items, item => item.Actor.UserId == bobProfile.UserId);
        Assert.Contains((await _client.GetFromJsonAsync<GroupDetailResponse>($"/api/v1/groups/{group.Id}"))!.Members, item => item.UserId == bobProfile.UserId);
        (await _client.DeleteAsync($"/api/v1/groups/{group.Id}/members/{bobProfile.UserId}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync($"/api/v1/groups/{group.Id}/members", new AddGroupMemberRequest(bobProfile.UserId))).StatusCode);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bob.AccessToken);
        Assert.Empty((await _client.GetFromJsonAsync<SocialProfileResponse[]>($"/api/v1/profiles/search?query={aliceUsername[..3]}"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync("/api/v1/friends/requests", new CreateFriendRequest(aliceUsername))).StatusCode);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", alice.AccessToken);
        (await _client.DeleteAsync($"/api/v1/blocks/{bobProfile.UserId}")).EnsureSuccessStatusCode();
        (await _client.DeleteAsync($"/api/v1/blocks/{bobProfile.UserId}")).EnsureSuccessStatusCode();
        Assert.Empty((await _client.GetFromJsonAsync<SocialProfileResponse[]>("/api/v1/blocks"))!);
        Assert.Empty((await _client.GetFromJsonAsync<SocialProfileResponse[]>("/api/v1/friends"))!);
        Assert.Contains((await _client.GetFromJsonAsync<SocialProfileResponse[]>($"/api/v1/profiles/search?query={bobUsername[..3]}"))!, item => item.UserId == bobProfile.UserId);
    }

    [Fact]
    public async Task Block_endpoints_require_authentication()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/blocks")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsync("/api/v1/blocks/user", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/api/v1/blocks/user")).StatusCode);
    }

    [Fact]
    public async Task Global_contests_are_visible_to_every_user_and_join_idempotently()
    {
        const string password = "waterly123";
        var firstEmail = $"contest-first-{Guid.NewGuid():N}@example.com";
        var secondEmail = $"contest-second-{Guid.NewGuid():N}@example.com";
        foreach (var email in new[] { firstEmail, secondEmail })
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        var first = await LoginAsync(firstEmail, password);
        var second = await LoginAsync(secondEmail, password);
        foreach (var accessToken in new[] { first.AccessToken, second.AccessToken })
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            (await _client.PutAsJsonAsync("/api/v1/me/onboarding", new CompleteOnboardingRequest(28, 178, 74.5m, 2000, ["habit"], "UTC"))).EnsureSuccessStatusCode();
        }
        Contest contest;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaterDbContext>();
            contest = new Contest("Seven days", new DateOnly(2026, 9, 16), 7, new DateTimeOffset(2026, 9, 15, 15, 0, 0, TimeSpan.Zero));
            db.Contests.Add(contest);
            await db.SaveChangesAsync();
        }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first.AccessToken);
        var listed = (await _client.GetFromJsonAsync<ContestResponse[]>("/api/v1/contests"))!.Single(item => item.Id == contest.Id);
        Assert.Equal("upcoming", listed.Status);
        Assert.Equal(new DateOnly(2026, 9, 23), listed.EndsOn);
        Assert.Equal(100, listed.DailyScoreCap);
        Assert.Equal(1, listed.ScoringRuleVersion);
        var operationId = Guid.NewGuid();
        var joined = await (await _client.PostAsJsonAsync($"/api/v1/contests/{contest.Id}/join", new JoinContestRequest(operationId))).Content.ReadFromJsonAsync<ContestResponse>();
        var repeated = await (await _client.PostAsJsonAsync($"/api/v1/contests/{contest.Id}/join", new JoinContestRequest(operationId))).Content.ReadFromJsonAsync<ContestResponse>();
        var anotherOperation = await (await _client.PostAsJsonAsync($"/api/v1/contests/{contest.Id}/join", new JoinContestRequest(Guid.NewGuid()))).Content.ReadFromJsonAsync<ContestResponse>();
        Assert.True(joined!.IsParticipant);
        Assert.Equal(1, repeated!.ParticipantCount);
        Assert.Equal(1, anotherOperation!.ParticipantCount);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", second.AccessToken);
        var detail = await _client.GetFromJsonAsync<ContestResponse>($"/api/v1/contests/{contest.Id}");
        Assert.False(detail!.IsParticipant);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/contests/{contest.Id}/score")).StatusCode);
        var secondJoin = await (await _client.PostAsJsonAsync($"/api/v1/contests/{contest.Id}/join", new JoinContestRequest(Guid.NewGuid()))).Content.ReadFromJsonAsync<ContestResponse>();
        Assert.Equal(2, secondJoin!.ParticipantCount);
    }

    [Fact]
    public async Task Contest_daily_score_uses_hydration_equivalent_join_date_and_final_snapshots()
    {
        const string password = "waterly123";
        var email = $"contest-score-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        var tokens = await LoginAsync(email, password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        (await _client.PutAsJsonAsync("/api/v1/me/onboarding", new CompleteOnboardingRequest(28, 178, 74.5m, 1000, ["habit"], "UTC"))).EnsureSuccessStatusCode();
        Contest contest;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaterDbContext>();
            contest = new Contest("Active contest", new DateOnly(2026, 9, 14), 7, new DateTimeOffset(2026, 9, 14, 0, 0, 0, TimeSpan.Zero));
            db.Contests.Add(contest);
            await db.SaveChangesAsync();
        }

        (await _client.PostAsJsonAsync($"/api/v1/contests/{contest.Id}/join", new JoinContestRequest(Guid.NewGuid()))).EnsureSuccessStatusCode();
        var hydration = await (await _client.PostAsJsonAsync("/api/v1/hydration/entries", new AddDrinkEntryRequest(Guid.NewGuid(), 1250, new DateTimeOffset(2026, 9, 15, 14, 0, 0, TimeSpan.Zero), "UTC", "coffee"))).Content.ReadFromJsonAsync<TodayHydrationResponse>();
        var score = await _client.GetFromJsonAsync<ContestScoreResponse>($"/api/v1/contests/{contest.Id}/score");
        Assert.Equal(100m, score!.TotalScore);
        Assert.Equal(100m, score.MaximumScore);
        Assert.Single(score.Days);
        Assert.Equal(new DateOnly(2026, 9, 15), score.Days.Single().Date);
        Assert.False(score.Days.Single().IsFinal);

        var entry = hydration!.Entries.Single();
        (await _client.PatchAsJsonAsync($"/api/v1/hydration/entries/{entry.Id}", new UpdateDrinkEntryRequest(625, "coffee", Guid.NewGuid()))).EnsureSuccessStatusCode();
        score = await _client.GetFromJsonAsync<ContestScoreResponse>($"/api/v1/contests/{contest.Id}/score");
        Assert.Equal(50m, score!.TotalScore);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            await scope.ServiceProvider.GetRequiredService<IContestScoreService>().FinalizeThroughAsync(user!.Id, new DateOnly(2026, 9, 15), CancellationToken.None);
        }
        (await _client.PatchAsJsonAsync($"/api/v1/hydration/entries/{entry.Id}", new UpdateDrinkEntryRequest(1250, "coffee", Guid.NewGuid()))).EnsureSuccessStatusCode();
        score = await _client.GetFromJsonAsync<ContestScoreResponse>($"/api/v1/contests/{contest.Id}/score");
        Assert.Equal(50m, score!.TotalScore);
        Assert.True(score.Days.Single().IsFinal);
    }

    [Fact]
    public async Task Contest_leaderboard_is_global_private_paginated_and_shares_tied_positions()
    {
        const string password = "waterly123";
        var users = new[]
        {
            ($"leader-a-{Guid.NewGuid():N}@example.com", "leader_a", "Leader A", 1000),
            ($"leader-b-{Guid.NewGuid():N}@example.com", "leader_b", "Leader B", 500),
            ($"leader-c-{Guid.NewGuid():N}@example.com", (string?)null, (string?)null, 500)
        };
        var tokens = new List<string>();
        foreach (var (email, username, displayName, volume) in users)
        {
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
            var session = await LoginAsync(email, password);
            tokens.Add(session.AccessToken);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
            (await _client.PutAsJsonAsync("/api/v1/me/onboarding", new CompleteOnboardingRequest(28, 178, 74.5m, 1000, ["habit"], "UTC"))).EnsureSuccessStatusCode();
            if (username is not null)
                (await _client.PutAsJsonAsync("/api/v1/profile", new UpdatePublicProfileRequest(username, displayName!, null))).EnsureSuccessStatusCode();
        }

        Contest contest;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaterDbContext>();
            contest = new Contest("Leaderboard", new DateOnly(2026, 9, 15), 7, new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero));
            db.Contests.Add(contest);
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.False(await scope.ServiceProvider.GetRequiredService<IContestFinalizationService>()
                .FinalizeAsync(contest.Id, CancellationToken.None));
        }

        for (var index = 0; index < users.Length; index++)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens[index]);
            (await _client.PostAsJsonAsync($"/api/v1/contests/{contest.Id}/join", new JoinContestRequest(Guid.NewGuid()))).EnsureSuccessStatusCode();
            (await _client.PostAsJsonAsync("/api/v1/hydration/entries", new AddDrinkEntryRequest(
                Guid.NewGuid(), users[index].Item4, new DateTimeOffset(2026, 9, 15, 14, 0, 0, TimeSpan.Zero), "UTC"))).EnsureSuccessStatusCode();
        }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens[1]);
        var leaderboard = await _client.GetFromJsonAsync<ContestLeaderboardResponse>($"/api/v1/contests/{contest.Id}/leaderboard?page=1&pageSize=10");
        Assert.NotNull(leaderboard);
        Assert.Equal(3, leaderboard.TotalCount);
        Assert.False(leaderboard.IsFinal);
        Assert.Equal([1, 2, 2], leaderboard.Entries.Select(item => item.Position));
        Assert.Equal(100m, leaderboard.Entries.First().TotalScore);
        Assert.False(leaderboard.Entries.First().IsTied);
        Assert.All(leaderboard.Entries.Where(item => item.Position == 2), item => Assert.True(item.IsTied));
        Assert.Contains(leaderboard.Entries, item => item.IsCurrentUser && item.Username == "leader_b");
        Assert.Contains(leaderboard.Entries, item => item.Username is null && item.DisplayName is null);

        var secondPage = await _client.GetFromJsonAsync<ContestLeaderboardResponse>($"/api/v1/contests/{contest.Id}/leaderboard?page=2&pageSize=2");
        Assert.NotNull(secondPage);
        Assert.Single(secondPage.Entries);
        Assert.Equal(2, secondPage.Entries.Single().Position);
        var json = await _client.GetStringAsync($"/api/v1/contests/{contest.Id}/leaderboard");
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("weight", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ended_contest_is_finalized_once_and_keeps_result_and_profile_snapshots()
    {
        const string password = "waterly123";
        var users = new[]
        {
            ($"final-a-{Guid.NewGuid():N}@example.com", "final_a", "Final A", 1000),
            ($"final-b-{Guid.NewGuid():N}@example.com", "final_b", "Final B", 500),
            ($"final-c-{Guid.NewGuid():N}@example.com", "final_c", "Final C", 500)
        };
        var accessTokens = new List<string>();
        var userIds = new List<string>();
        foreach (var (email, username, displayName, _) in users)
        {
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
            var session = await LoginAsync(email, password);
            accessTokens.Add(session.AccessToken);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
            (await _client.PutAsJsonAsync("/api/v1/me/onboarding", new CompleteOnboardingRequest(28, 178, 74.5m, 1000, ["habit"], "UTC"))).EnsureSuccessStatusCode();
            (await _client.PutAsJsonAsync("/api/v1/profile", new UpdatePublicProfileRequest(username, displayName, null))).EnsureSuccessStatusCode();
        }

        Contest contest;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaterDbContext>();
            userIds.AddRange(await db.Users.Where(item => users.Select(user => user.Item1).Contains(item.Email!)).OrderBy(item => item.Email).Select(item => item.Id).ToArrayAsync());
            contest = new Contest("Final contest", new DateOnly(2026, 9, 8), 7, new DateTimeOffset(2026, 9, 8, 0, 0, 0, TimeSpan.Zero));
            db.Contests.Add(contest);
            for (var index = 0; index < userIds.Count; index++)
            {
                db.HydrationGoals.Add(new HydrationGoal(userIds[index], 1000, new DateOnly(2026, 9, 14)));
                db.ContestParticipants.Add(new ContestParticipant(contest.Id, userIds[index], Guid.NewGuid(), new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero), new DateOnly(2026, 9, 14)));
                db.DrinkEntries.Add(new DrinkEntry(userIds[index], Guid.NewGuid(), users[index].Item4, new DateTimeOffset(2026, 9, 14, 14, 0, 0, TimeSpan.Zero), "UTC"));
            }
            await db.SaveChangesAsync();
        }

        var notificationInstallationId = Guid.NewGuid();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessTokens[0]);
        (await _client.PutAsJsonAsync($"/api/v1/notifications/installations/{notificationInstallationId}",
            new UpsertDeviceInstallationRequest("ExponentPushToken[final-a]", "android", "pt-BR"))).EnsureSuccessStatusCode();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IContestFinalizationService>();
            Assert.Contains(contest.Id, await service.GetDueAsync(20, CancellationToken.None));
            Assert.True(await service.FinalizeAsync(contest.Id, CancellationToken.None));
            Assert.True(await service.FinalizeAsync(contest.Id, CancellationToken.None));
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IContestRewardService>();
            Assert.Contains(contest.Id, await service.GetDueAsync(20, CancellationToken.None));
            await service.GrantAsync(contest.Id, CancellationToken.None);
            await service.GrantAsync(contest.Id, CancellationToken.None);
            var db = scope.ServiceProvider.GetRequiredService<WaterDbContext>();
            Assert.Single(await db.ContestRewardCheckpoints.Where(item => item.ContestId == contest.Id).ToArrayAsync());
            Assert.Equal(6, await db.DropsLedgerEntries.CountAsync(item => item.ReferenceType == "contest" && item.ReferenceId == contest.Id.ToString()));
            Assert.Equal(6, await db.PrestigeLedgerEntries.CountAsync(item => item.ReferenceType == "contest" && item.ReferenceId == contest.Id.ToString()));
            var medalId = await db.MedalDefinitions.Where(item => item.ContestId == contest.Id).Select(item => item.Id).SingleAsync();
            Assert.Equal(3, await db.UserMedals.CountAsync(item => item.MedalDefinitionId == medalId));
            var push = await db.PushNotificationMessages.Include(item => item.DeviceInstallation).SingleAsync(item => item.DeviceInstallationId == notificationInstallationId);
            Assert.Equal("contest-result", push.EventType);
            Assert.Contains("125 Drops", push.Body);
            Assert.Contains(contest.Id.ToString(), push.DataJson);
        }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessTokens[0]);
        (await _client.PutAsJsonAsync("/api/v1/profile", new UpdatePublicProfileRequest("renamed_a", "Renamed A", null))).EnsureSuccessStatusCode();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaterDbContext>();
            db.DrinkEntries.Add(new DrinkEntry(userIds[1], Guid.NewGuid(), 500, new DateTimeOffset(2026, 9, 14, 15, 0, 0, TimeSpan.Zero), "UTC"));
            await db.SaveChangesAsync();
            Assert.Equal(1, await db.ContestFinalizations.CountAsync(item => item.ContestId == contest.Id));
            Assert.Equal(3, await db.ContestResults.CountAsync(item => item.ContestId == contest.Id));
        }

        var leaderboard = await _client.GetFromJsonAsync<ContestLeaderboardResponse>($"/api/v1/contests/{contest.Id}/leaderboard");
        Assert.NotNull(leaderboard);
        Assert.True(leaderboard.IsFinal);
        Assert.Equal([1, 2, 2], leaderboard.Entries.Select(item => item.Position));
        Assert.Equal([100m, 50m, 50m], leaderboard.Entries.Select(item => item.TotalScore));
        Assert.Equal(125, leaderboard.Entries.Single(item => item.Position == 1).DropsReward);
        Assert.All(leaderboard.Entries.Where(item => item.Position == 2), item => Assert.Equal(85, item.DropsReward));
        Assert.All(leaderboard.Entries.Where(item => item.Position == 2), item => Assert.Equal(2, item.MedalPosition));
        Assert.Contains(leaderboard.Entries, item => item.Username == "final_a" && item.DisplayName == "Final A");
        Assert.DoesNotContain(leaderboard.Entries, item => item.Username == "renamed_a");

        var medals = await _client.GetFromJsonAsync<MedalResponse[]>("/api/v1/medals");
        Assert.NotNull(medals);
        Assert.Contains(medals, item => item.ContestId == contest.Id && item.Name == "Final contest" && item.Position == 1);
        var wallet = await _client.GetFromJsonAsync<ProgressionBalanceResponse>("/api/v1/wallet");
        Assert.NotNull(wallet);
        Assert.Equal(125, wallet.Entries.Where(item => item.ReferenceId == contest.Id.ToString()).Sum(item => item.Amount));
        Assert.All(wallet.Entries.Where(item => item.ReferenceId == contest.Id.ToString()), item => Assert.Equal("Final contest", item.ReferenceLabel));

        Contest emptyContest;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaterDbContext>();
            emptyContest = new Contest("Empty final", new DateOnly(2026, 9, 8), 7, new DateTimeOffset(2026, 9, 8, 0, 0, 0, TimeSpan.Zero));
            db.Contests.Add(emptyContest);
            await db.SaveChangesAsync();
            Assert.True(await scope.ServiceProvider.GetRequiredService<IContestFinalizationService>().FinalizeAsync(emptyContest.Id, CancellationToken.None));
            Assert.Equal(0, (await db.ContestFinalizations.SingleAsync(item => item.ContestId == emptyContest.Id)).ParticipantCount);
        }

        var zeroEmail = $"zero-score-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email = zeroEmail, password });
        var zeroSession = await LoginAsync(zeroEmail, password);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", zeroSession.AccessToken);
        (await _client.PutAsJsonAsync("/api/v1/me/onboarding", new CompleteOnboardingRequest(28, 178, 74.5m, 1000, ["habit"], "UTC"))).EnsureSuccessStatusCode();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaterDbContext>();
            var zeroUserId = await db.Users.Where(item => item.Email == zeroEmail).Select(item => item.Id).SingleAsync();
            var zeroContest = new Contest("Zero score final", new DateOnly(2026, 9, 8), 7, new DateTimeOffset(2026, 9, 8, 0, 0, 0, TimeSpan.Zero));
            db.Contests.Add(zeroContest);
            db.HydrationGoals.Add(new HydrationGoal(zeroUserId, 1000, new DateOnly(2026, 9, 14)));
            db.ContestParticipants.Add(new ContestParticipant(zeroContest.Id, zeroUserId, Guid.NewGuid(), new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero), new DateOnly(2026, 9, 14)));
            await db.SaveChangesAsync();
            Assert.True(await scope.ServiceProvider.GetRequiredService<IContestFinalizationService>().FinalizeAsync(zeroContest.Id, CancellationToken.None));
            await scope.ServiceProvider.GetRequiredService<IContestRewardService>().GrantAsync(zeroContest.Id, CancellationToken.None);
            Assert.Equal(0m, (await db.ContestResults.SingleAsync(item => item.ContestId == zeroContest.Id)).TotalScore);
            Assert.False(await db.DropsLedgerEntries.AnyAsync(item => item.ReferenceType == "contest" && item.ReferenceId == zeroContest.Id.ToString()));
            var zeroMedalId = await db.MedalDefinitions.Where(item => item.ContestId == zeroContest.Id).Select(item => item.Id).SingleAsync();
            Assert.False(await db.UserMedals.AnyAsync(item => item.MedalDefinitionId == zeroMedalId));
        }
    }

    [Fact]
    public async Task Only_configured_administrator_can_publish_global_contests()
    {
        const string password = "waterly123";
        const string adminEmail = "contest-admin@example.com";
        var userEmail = $"contest-user-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email = adminEmail, password });
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email = userEmail, password });
        var admin = await LoginAsync(adminEmail, password);
        var user = await LoginAsync(userEmail, password);
        var request = new CreateContestRequest("Community challenge", new DateOnly(2026, 9, 16), 7);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        Assert.False((await _client.GetFromJsonAsync<ContestCapabilitiesResponse>("/api/v1/contests/capabilities"))!.CanManageContests);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync("/api/v1/admin/contests", request)).StatusCode);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", admin.AccessToken);
        Assert.True((await _client.GetFromJsonAsync<ContestCapabilitiesResponse>("/api/v1/contests/capabilities"))!.CanManageContests);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/v1/admin/contests", request with { DurationDays = 8 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/v1/admin/contests", request with { StartsOn = new DateOnly(2026, 9, 14) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/v1/admin/contests", request with { Name = "x" })).StatusCode);
        var response = await _client.PostAsJsonAsync("/api/v1/admin/contests", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ContestResponse>();
        Assert.NotNull(created);
        Assert.Equal(new DateOnly(2026, 9, 23), created.EndsOn);
        Assert.Equal(100, created.DailyScoreCap);
        Assert.Equal(1, created.ScoringRuleVersion);
        Assert.Equal(1, created.RewardRuleVersion);
        Assert.Contains((await _client.GetFromJsonAsync<ContestResponse[]>("/api/v1/contests"))!, item => item.Id == created.Id);
    }

    [Fact]
    public async Task Contest_endpoints_require_authentication()
    {
        using var client = factory.CreateClient();
        var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/contests")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/contests/capabilities")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/admin/contests", new CreateContestRequest("Contest", new DateOnly(2026, 9, 16), 7))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/v1/contests/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/v1/contests/{id}/score")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/v1/contests/{id}/leaderboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/medals")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync($"/api/v1/contests/{id}/join", new JoinContestRequest(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync($"/api/v1/notifications/installations/{id}", new UpsertDeviceInstallationRequest("ExponentPushToken[token]", "android", "pt-BR"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync($"/api/v1/notifications/installations/{id}")).StatusCode);
    }

    [Fact]
    public async Task Push_installation_is_owned_idempotent_and_disabled_after_invalid_receipt()
    {
        const string password = "waterly123";
        var firstEmail = $"push-a-{Guid.NewGuid():N}@example.com";
        var secondEmail = $"push-b-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email = firstEmail, password });
        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email = secondEmail, password });
        var first = await LoginAsync(firstEmail, password);
        var second = await LoginAsync(secondEmail, password);
        var installationId = Guid.NewGuid();
        var request = new UpsertDeviceInstallationRequest("ExponentPushToken[push-test]", "android", "pt-BR");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first.AccessToken);
        (await _client.PutAsJsonAsync($"/api/v1/notifications/installations/{installationId}", request)).EnsureSuccessStatusCode();
        (await _client.PutAsJsonAsync($"/api/v1/notifications/installations/{installationId}", request)).EnsureSuccessStatusCode();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", second.AccessToken);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PutAsJsonAsync($"/api/v1/notifications/installations/{installationId}", request)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WaterDbContext>();
        var now = new DateTimeOffset(2026, 9, 15, 15, 0, 0, TimeSpan.Zero);
        db.PushNotificationMessages.Add(new PushNotificationMessage(installationId, "contest-result", Guid.NewGuid().ToString(), "Result", "Body", "{}", "test-result", now));
        await db.SaveChangesAsync();
        var gateway = new TestPushGateway();
        var options = Options.Create(new PushNotificationOptions { Enabled = true, BatchSize = 10, MaxAttempts = 3, ReceiptDelay = TimeSpan.FromSeconds(1) });
        await new PushNotificationProcessor(db, gateway, options, new FixedTimeProvider(now)).ProcessAsync(CancellationToken.None);
        await new PushNotificationProcessor(db, gateway, options, new FixedTimeProvider(now.AddMinutes(1))).ProcessAsync(CancellationToken.None);

        var installation = await db.DeviceInstallations.SingleAsync(item => item.Id == installationId);
        var message = await db.PushNotificationMessages.SingleAsync(item => item.DeviceInstallationId == installationId);
        Assert.NotNull(installation.DisabledAt);
        Assert.NotNull(message.CompletedAt);
        Assert.Equal("DeviceNotRegistered", message.LastError);
    }

    [Fact]
    public async Task Private_group_enforces_membership_ownership_and_friendship()
    {
        const string password = "waterly123";
        var ownerEmail = $"owner-{Guid.NewGuid():N}@example.com";
        var memberEmail = $"member-{Guid.NewGuid():N}@example.com";
        var outsiderEmail = $"outsider-{Guid.NewGuid():N}@example.com";
        foreach (var email in new[] { ownerEmail, memberEmail, outsiderEmail })
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        var owner = await LoginAsync(ownerEmail, password);
        var member = await LoginAsync(memberEmail, password);
        var outsider = await LoginAsync(outsiderEmail, password);

        async Task SaveProfile(string token, string username)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            (await _client.PutAsJsonAsync("/api/v1/profile", new UpdatePublicProfileRequest(username, username, null))).EnsureSuccessStatusCode();
        }
        await SaveProfile(owner.AccessToken, "group_owner");
        await SaveProfile(member.AccessToken, "group_member");
        await SaveProfile(outsider.AccessToken, "group_outsider");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.AccessToken);
        await _client.PostAsJsonAsync("/api/v1/friends/requests", new CreateFriendRequest("group_member"));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", member.AccessToken);
        await _client.PostAsJsonAsync("/api/v1/friends/requests", new CreateFriendRequest("group_owner"));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.AccessToken);
        var createResponse = await _client.PostAsJsonAsync("/api/v1/groups", new SaveGroupRequest("Hydration Team", "Private group"));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<GroupDetailResponse>();
        Assert.NotNull(created);
        Assert.True(created.IsOwner);
        Assert.Single(created.Members);
        var memberProfile = (await _client.GetFromJsonAsync<SocialProfileResponse[]>("/api/v1/friends"))!.Single();
        var added = await (await _client.PostAsJsonAsync($"/api/v1/groups/{created.Id}/members", new AddGroupMemberRequest(memberProfile.UserId))).Content.ReadFromJsonAsync<GroupDetailResponse>();
        var duplicate = await (await _client.PostAsJsonAsync($"/api/v1/groups/{created.Id}/members", new AddGroupMemberRequest(memberProfile.UserId))).Content.ReadFromJsonAsync<GroupDetailResponse>();
        Assert.Equal(2, added?.Members.Count);
        Assert.Equal(2, duplicate?.Members.Count);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", member.AccessToken);
        var memberFeed = await _client.GetFromJsonAsync<FeedPageResponse>("/api/v1/feed");
        Assert.Single(memberFeed!.Items, item => item.Type == "group-joined" && item.Actor.IsCurrentUser);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", outsider.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/groups/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync($"/api/v1/groups/{created.Id}", new SaveGroupRequest("Changed", null))).StatusCode);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", member.AccessToken);
        var memberViewJson = await _client.GetStringAsync($"/api/v1/groups/{created.Id}");
        Assert.DoesNotContain("email", memberViewJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("weight", memberViewJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/v1/groups/{created.Id}/membership")).StatusCode);
        Assert.DoesNotContain((await _client.GetFromJsonAsync<FeedPageResponse>("/api/v1/feed"))!.Items, item => item.GroupId == created.Id);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.AccessToken);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.DeleteAsync($"/api/v1/groups/{created.Id}/membership")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/v1/groups/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Groups_require_authentication()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/groups")).StatusCode);
    }

    [Fact]
    public async Task Feed_visibility_tracks_current_friendship_and_paginates_without_duplicates()
    {
        const string password = "waterly123";
        var aliceEmail = $"feed-alice-{Guid.NewGuid():N}@example.com";
        var bobEmail = $"feed-bob-{Guid.NewGuid():N}@example.com";
        var outsiderEmail = $"feed-outsider-{Guid.NewGuid():N}@example.com";
        foreach (var email in new[] { aliceEmail, bobEmail, outsiderEmail }) await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        var alice = await LoginAsync(aliceEmail, password);
        var bob = await LoginAsync(bobEmail, password);
        var outsider = await LoginAsync(outsiderEmail, password);
        async Task Profile(string accessToken, string username)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            (await _client.PutAsJsonAsync("/api/v1/profile", new UpdatePublicProfileRequest(username, username, null))).EnsureSuccessStatusCode();
        }
        var aliceUsername = $"alice_{Guid.NewGuid():N}"[..20];
        var bobUsername = $"bob_{Guid.NewGuid():N}"[..20];
        var outsiderUsername = $"out_{Guid.NewGuid():N}"[..20];
        await Profile(alice.AccessToken, aliceUsername);
        await Profile(bob.AccessToken, bobUsername);
        await Profile(outsider.AccessToken, outsiderUsername);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", alice.AccessToken);
        var bobProfile = (await _client.GetFromJsonAsync<SocialProfileResponse[]>($"/api/v1/profiles/search?query={bobUsername}"))!.Single();
        await _client.PostAsJsonAsync("/api/v1/friends/requests", new CreateFriendRequest(bobProfile.Username));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bob.AccessToken);
        (await _client.PostAsJsonAsync("/api/v1/friends/requests", new CreateFriendRequest(aliceUsername))).EnsureSuccessStatusCode();

        string aliceId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaterDbContext>();
            aliceId = (await db.Users.SingleAsync(item => item.Email == aliceEmail)).Id;
            var now = new DateTimeOffset(2026, 9, 15, 14, 30, 0, TimeSpan.Zero);
            db.FeedEvents.AddRange(
                new FeedEvent(aliceId, "achievement-unlocked", "friends", "a", "a", "feed-a", now),
                new FeedEvent(aliceId, "achievement-unlocked", "friends", "b", "b", "feed-b", now.AddMilliseconds(1)));
            await db.SaveChangesAsync();
        }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bob.AccessToken);
        var first = await _client.GetFromJsonAsync<FeedPageResponse>("/api/v1/feed?limit=1");
        var second = await _client.GetFromJsonAsync<FeedPageResponse>($"/api/v1/feed?limit=1&cursor={Uri.EscapeDataString(first!.NextCursor!)}");
        Assert.Single(first.Items);
        Assert.Single(second!.Items);
        Assert.NotEqual(first.Items.Single().Id, second.Items.Single().Id);
        var eventId = first.Items.Single().Id;
        var water = await (await _client.PutAsJsonAsync($"/api/v1/feed/{eventId}/reactions/me", new SetFeedReactionRequest("water"))).Content.ReadFromJsonAsync<FeedReactionSummaryResponse>();
        Assert.Equal(1, water!.Counts["water"]);
        Assert.Equal("water", water.CurrentUserReaction);
        var repeated = await (await _client.PutAsJsonAsync($"/api/v1/feed/{eventId}/reactions/me", new SetFeedReactionRequest("water"))).Content.ReadFromJsonAsync<FeedReactionSummaryResponse>();
        Assert.Equal(1, repeated!.Counts["water"]);
        var changed = await (await _client.PutAsJsonAsync($"/api/v1/feed/{eventId}/reactions/me", new SetFeedReactionRequest("fire"))).Content.ReadFromJsonAsync<FeedReactionSummaryResponse>();
        Assert.False(changed!.Counts.ContainsKey("water"));
        Assert.Equal(1, changed.Counts["fire"]);
        var removed = await (await _client.DeleteAsync($"/api/v1/feed/{eventId}/reactions/me")).Content.ReadFromJsonAsync<FeedReactionSummaryResponse>();
        Assert.Empty(removed!.Counts);
        Assert.Null(removed.CurrentUserReaction);
        (await _client.PutAsJsonAsync($"/api/v1/feed/{eventId}/reactions/me", new SetFeedReactionRequest("celebrate"))).EnsureSuccessStatusCode();
        var reactedFeed = await _client.GetFromJsonAsync<FeedPageResponse>("/api/v1/feed");
        var reactedEvent = reactedFeed!.Items.Single(item => item.Id == eventId);
        Assert.Equal(1, reactedEvent.Reactions.Counts["celebrate"]);
        Assert.Equal("celebrate", reactedEvent.Reactions.CurrentUserReaction);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync($"/api/v1/feed/{eventId}/reactions/me", new SetFeedReactionRequest("unknown"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/v1/feed?cursor=invalid")).StatusCode);
        var json = await _client.GetStringAsync("/api/v1/feed");
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("weight", json, StringComparison.OrdinalIgnoreCase);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", outsider.AccessToken);
        Assert.Empty((await _client.GetFromJsonAsync<FeedPageResponse>("/api/v1/feed"))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync($"/api/v1/feed/{eventId}/reactions/me", new SetFeedReactionRequest("water"))).StatusCode);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", alice.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync($"/api/v1/feed/{eventId}/reactions/me", new SetFeedReactionRequest("water"))).StatusCode);
        (await _client.DeleteAsync($"/api/v1/friends/{bobProfile.UserId}")).EnsureSuccessStatusCode();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bob.AccessToken);
        Assert.Empty((await _client.GetFromJsonAsync<FeedPageResponse>("/api/v1/feed"))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/v1/feed/{eventId}/reactions/me")).StatusCode);
    }

    [Fact]
    public async Task Feed_requires_authentication()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/feed")).StatusCode);
        var eventId = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync($"/api/v1/feed/{eventId}/reactions/me", new SetFeedReactionRequest("water"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync($"/api/v1/feed/{eventId}/reactions/me")).StatusCode);
    }

    [Fact]
    public async Task Group_invites_rotate_are_idempotent_and_respect_capacity()
    {
        const string password = "waterly123";
        var ownerEmail = $"invite-owner-{Guid.NewGuid():N}@example.com";
        var memberEmail = $"invite-member-{Guid.NewGuid():N}@example.com";
        foreach (var email in new[] { ownerEmail, memberEmail })
            await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password });
        var owner = await LoginAsync(ownerEmail, password);
        var member = await LoginAsync(memberEmail, password);

        async Task SaveProfile(string accessToken, string username, string displayName)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            (await _client.PutAsJsonAsync("/api/v1/profile", new UpdatePublicProfileRequest(username, displayName, null))).EnsureSuccessStatusCode();
        }
        await SaveProfile(owner.AccessToken, $"owner_{Guid.NewGuid():N}"[..20], "Invite Owner");
        await SaveProfile(member.AccessToken, $"member_{Guid.NewGuid():N}"[..20], "Invite Member");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.AccessToken);
        var group = await (await _client.PostAsJsonAsync("/api/v1/groups", new SaveGroupRequest("Invite Group", null))).Content.ReadFromJsonAsync<GroupDetailResponse>();
        Assert.NotNull(group);
        var firstResponse = await _client.PostAsync($"/api/v1/groups/{group.Id}/invite", null);
        firstResponse.EnsureSuccessStatusCode();
        var first = await firstResponse.Content.ReadFromJsonAsync<GroupInviteResponse>();
        var secondResponse = await _client.PostAsync($"/api/v1/groups/{group.Id}/invite", null);
        secondResponse.EnsureSuccessStatusCode();
        var second = await secondResponse.Content.ReadFromJsonAsync<GroupInviteResponse>();
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first.Token, second.Token);

        _client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/invites/group/{first.Token}")).StatusCode);
        var preview = await _client.GetFromJsonAsync<GroupInvitePreviewResponse>($"/api/v1/invites/group/{second.Token}");
        Assert.Equal("Invite Group", preview?.GroupName);
        Assert.Equal("Invite Owner", preview?.OwnerDisplayName);
        Assert.False(preview?.IsMember);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsync($"/api/v1/invites/group/{second.Token}/accept", null)).StatusCode);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", member.AccessToken);
        var accepted = await (await _client.PostAsync($"/api/v1/invites/group/{second.Token}/accept", null)).Content.ReadFromJsonAsync<GroupDetailResponse>();
        var repeated = await (await _client.PostAsync($"/api/v1/invites/group/{second.Token}/accept", null)).Content.ReadFromJsonAsync<GroupDetailResponse>();
        Assert.Equal(2, accepted?.Members.Count);
        Assert.Equal(2, repeated?.Members.Count);

        var ownGroup = await _client.PostAsJsonAsync("/api/v1/groups", new SaveGroupRequest("Second Slot", null));
        ownGroup.EnsureSuccessStatusCode();
        var third = await _client.PostAsJsonAsync("/api/v1/groups", new SaveGroupRequest("No Slot", null));
        Assert.Equal(HttpStatusCode.Conflict, third.StatusCode);
        Assert.Contains("group_capacity_reached", await third.Content.ReadAsStringAsync());
        var capacity = await _client.GetFromJsonAsync<GroupCapacityResponse>("/api/v1/groups/capacity");
        Assert.Equal(2, capacity?.Used);
        Assert.Equal(2, capacity?.Limit);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/v1/groups/{group.Id}/invite")).StatusCode);
        _client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/invites/group/{second.Token}")).StatusCode);
    }

    private sealed class TestPushGateway : IExpoPushGateway
    {
        public Task<IReadOnlyCollection<ExpoPushResult>> SendAsync(IReadOnlyCollection<ExpoPushEnvelope> messages, CancellationToken token) =>
            Task.FromResult<IReadOnlyCollection<ExpoPushResult>>(messages.Select(_ => new ExpoPushResult("ok", "ticket-1", null, null)).ToArray());

        public Task<IReadOnlyDictionary<string, ExpoPushResult>> GetReceiptsAsync(IReadOnlyCollection<string> ticketIds, CancellationToken token) =>
            Task.FromResult<IReadOnlyDictionary<string, ExpoPushResult>>(ticketIds.ToDictionary(item => item, _ => new ExpoPushResult("error", null, "DeviceNotRegistered", "unregistered")));
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
