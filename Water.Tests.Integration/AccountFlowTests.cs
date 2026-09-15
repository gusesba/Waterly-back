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
            new AddDrinkEntryRequest(Guid.NewGuid(), 500, DateTimeOffset.UtcNow, "UTC"));
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

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", outsider.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/groups/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync($"/api/v1/groups/{created.Id}", new SaveGroupRequest("Changed", null))).StatusCode);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", member.AccessToken);
        var memberViewJson = await _client.GetStringAsync($"/api/v1/groups/{created.Id}");
        Assert.DoesNotContain("email", memberViewJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("weight", memberViewJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/v1/groups/{created.Id}/membership")).StatusCode);

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
