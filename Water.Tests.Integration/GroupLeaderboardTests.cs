using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Water.Application.Hydration;
using Water.Application.Profiles;
using Water.Application.Social;
using Water.Infrastructure.Persistence;

namespace Water.Tests.Integration;

public sealed class GroupLeaderboardTests(WaterApiFactory factory) : IClassFixture<WaterApiFactory>
{
    [Fact]
    public async Task Ranking_caps_daily_scores_preserves_ties_paginates_and_updates_after_edits()
    {
        using var owner = await CreateMemberAsync(2000, "Owner");
        using var member = await CreateMemberAsync(1000, "Member");
        var group = await CreateGroupAsync(owner, member);
        var first = await AddAsync(owner, 2000);
        await AddAsync(owner, 2000);
        var memberEntry = await AddAsync(member, 1000);
        var path = $"/api/v1/groups/{group.Id}/leaderboard";
        var ranking = await owner.GetFromJsonAsync<GroupLeaderboardResponse>(path);
        Assert.NotNull(ranking);
        Assert.Equal(2, ranking.TotalCount);
        Assert.All(ranking.Entries, row => { Assert.Equal(100m, row.TotalScore); Assert.Equal(1, row.Position); Assert.True(row.IsTied); });
        Assert.Single(ranking.Entries, row => row.IsCurrentUser);
        var page = await owner.GetFromJsonAsync<GroupLeaderboardResponse>(path + "?page=2&pageSize=1");
        Assert.Single(page!.Entries);
        var empty = await owner.GetFromJsonAsync<GroupLeaderboardResponse>(path + "?page=3&pageSize=1");
        Assert.Empty(empty!.Entries);

        (await member.PatchAsJsonAsync($"/api/v1/hydration/entries/{memberEntry}", new UpdateDrinkEntryRequest(500, "water", Guid.NewGuid()))).EnsureSuccessStatusCode();
        ranking = await owner.GetFromJsonAsync<GroupLeaderboardResponse>(path);
        Assert.Equal([100m, 50m], ranking!.Entries.Select(row => row.TotalScore).ToArray());
        Assert.Equal([1, 2], ranking.Entries.Select(row => row.Position).ToArray());
        (await member.DeleteAsync($"/api/v1/hydration/entries/{memberEntry}?clientOperationId={Guid.NewGuid()}")).EnsureSuccessStatusCode();
        ranking = await owner.GetFromJsonAsync<GroupLeaderboardResponse>(path);
        Assert.Equal(0m, ranking!.Entries.Last().TotalScore);
        var json = await owner.GetStringAsync(path);
        foreach (var secret in new[] { "userId", "email", "dailyTargetMl", "hydrationMl", "weightKg", "heightCm" })
            Assert.DoesNotContain(secret, json);
        Assert.NotEqual(Guid.Empty, first);
    }

    [Fact]
    public async Task Ranking_requires_membership_and_valid_pagination_and_keeps_zero_score_members()
    {
        using var owner = await CreateMemberAsync(2000, "Owner");
        using var member = await CreateMemberAsync(2000, null);
        using var outsider = await CreateMemberAsync(2000, "Outsider");
        using var anonymous = factory.CreateClient();
        var group = await CreateGroupAsync(owner, member);
        var path = $"/api/v1/groups/{group.Id}/leaderboard";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(path)).StatusCode);
        foreach (var query in new[] { "?page=0", "?page=-1", "?pageSize=51", "?page=2147483647" })
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync(path + query)).StatusCode);
        var ranking = await member.GetFromJsonAsync<GroupLeaderboardResponse>(path);
        Assert.Equal(2, ranking!.Entries.Count);
        Assert.Contains(ranking.Entries, row => row.IsCurrentUser && row.Username == null && row.TotalScore == 0);
        (await member.DeleteAsync($"/api/v1/groups/{group.Id}/membership")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync(path)).StatusCode);
    }

    private async Task<HttpClient> CreateMemberAsync(int target, string? name)
    {
        var client = factory.CreateClient();
        var email = $"ranking-{Guid.NewGuid():N}@example.com";
        (await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "waterly123" })).EnsureSuccessStatusCode();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login?useCookies=false", new { email, password = "waterly123" });
        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        (await client.PutAsJsonAsync("/api/v1/me/onboarding", new CompleteOnboardingRequest(28, 178, 74.5m, target, ["habit"], "UTC"))).EnsureSuccessStatusCode();
        if (name != null)
            (await client.PutAsJsonAsync("/api/v1/profile", new UpdatePublicProfileRequest($"rank_{Guid.NewGuid():N}"[..20], name, null))).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<GroupDetailResponse> CreateGroupAsync(HttpClient owner, HttpClient member)
    {
        var response = await owner.PostAsJsonAsync("/api/v1/groups", new SaveGroupRequest("Ranking Group", null));
        response.EnsureSuccessStatusCode();
        var group = (await response.Content.ReadFromJsonAsync<GroupDetailResponse>())!;
        var invite = await (await owner.PostAsync($"/api/v1/groups/{group.Id}/invite", null)).Content.ReadFromJsonAsync<GroupInviteResponse>();
        (await member.PostAsync($"/api/v1/invites/group/{invite!.Token}/accept", null)).EnsureSuccessStatusCode();
        return group;
    }

    private static async Task<Guid> AddAsync(HttpClient client, int volume)
    {
        var id = Guid.NewGuid();
        var payload = new AddDrinkEntryRequest(id, volume, new DateTimeOffset(2026, 9, 15, 14, 0, 0, TimeSpan.Zero), "UTC");
        (await client.PostAsJsonAsync("/api/v1/hydration/entries", payload)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/v1/hydration/entries", payload)).EnsureSuccessStatusCode();
        return id;
    }
}
