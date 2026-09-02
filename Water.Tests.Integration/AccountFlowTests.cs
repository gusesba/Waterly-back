using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Water.Application.Profiles;

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
    }

    [Fact]
    public async Task Profile_requires_authentication()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/me");

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
