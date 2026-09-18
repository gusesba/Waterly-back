using System.Threading.RateLimiting;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authorization;
using Water.Api.Features.Profiles;
using Water.Api.Features.Hydration;
using Water.Api.Features.Habits;
using Water.Api.Features.Achievements;
using Water.Api.Features.Progression;
using Water.Api.Features.Cosmetics;
using Water.Api.Features.Social;
using Water.Api.Features.Feed;
using Water.Api.Features.Competition;
using Water.Api.Features.Notifications;
using Water.Infrastructure;
using Water.Infrastructure.Identity;
using Water.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddValidation();
builder.Services.AddAuthorization(options => options.AddPolicy(ContestEndpoints.AdminPolicy, policy =>
    policy.RequireAuthenticatedUser().AddRequirements(new ContestAdminRequirement())));
builder.Services.AddScoped<IAuthorizationHandler, ContestAdminAuthorizationHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin();
        }
        else if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins);
        }

        policy.AllowAnyHeader().AllowAnyMethod();
    });
});
var rateLimits = builder.Environment.IsEnvironment("Testing")
    ? new RateLimitSettings
    {
        AuthPermitLimit = 10000,
        SocialPermitLimit = 10000,
        ContestReadPermitLimit = 10000,
        ContestWritePermitLimit = 10000,
        GlobalConcurrencyLimit = 10000
    }
    : builder.Environment.IsEnvironment("RateLimitTesting")
    ? new RateLimitSettings
    {
        AuthPermitLimit = 10000,
        SocialPermitLimit = 10000,
        ContestReadPermitLimit = 2,
        ContestWritePermitLimit = 10000,
        GlobalConcurrencyLimit = 10000
    }
    : builder.Configuration.GetSection(RateLimitSettings.SectionName).Get<RateLimitSettings>() ?? new RateLimitSettings();
builder.Services.AddOptions<RateLimitSettings>()
    .Bind(builder.Configuration.GetSection(RateLimitSettings.SectionName))
    .Validate(settings => settings.AuthPermitLimit > 0 && settings.SocialPermitLimit > 0 &&
        settings.ContestReadPermitLimit > 0 && settings.ContestWritePermitLimit > 0 &&
        settings.WindowSeconds > 0 && settings.GlobalConcurrencyLimit > 0,
        "Rate limit values must be positive.")
    .ValidateOnStart();
builder.Services.AddRateLimiter(options =>
{
    var window = TimeSpan.FromSeconds(rateLimits.WindowSeconds);
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
        RateLimitPartition.GetConcurrencyLimiter("global", _ => new ConcurrencyLimiterOptions
        {
            PermitLimit = rateLimits.GlobalConcurrencyLimit,
            QueueLimit = 0
        }));
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
        _ => FixedWindow(rateLimits.AuthPermitLimit, window)));
    options.AddPolicy("social", context => RateLimitPartition.GetFixedWindowLimiter(
        ClientKey(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimits.SocialPermitLimit,
            Window = window,
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("contest-read", context => RateLimitPartition.GetFixedWindowLimiter(
        ClientKey(context),
        _ => FixedWindow(rateLimits.ContestReadPermitLimit, window)));
    options.AddPolicy("contest-write", context => RateLimitPartition.GetFixedWindowLimiter(
        ClientKey(context),
        _ => FixedWindow(rateLimits.ContestWritePermitLimit, window)));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests."
        }, token);
    };
});
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing") && !app.Environment.IsEnvironment("RateLimitTesting"))
{
    app.UseHttpsRedirection();
}
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
var authEndpoints = app.MapGroup("/api/v1/auth");
if (!app.Environment.IsEnvironment("Testing"))
{
    authEndpoints.RequireRateLimiting("auth");
}
authEndpoints.MapIdentityApi<ApplicationUser>();
app.MapProfileEndpoints();
app.MapPublicProfileEndpoints();
app.MapHydrationEndpoints();
app.MapHabitEndpoints();
app.MapAchievementEndpoints();
app.MapProgressionEndpoints();
app.MapCosmeticEndpoints();
app.MapFriendEndpoints(!app.Environment.IsEnvironment("Testing"));
app.MapBlockEndpoints(!app.Environment.IsEnvironment("Testing"));
app.MapGroupEndpoints(!app.Environment.IsEnvironment("Testing"));
app.MapFeedEndpoints(!app.Environment.IsEnvironment("Testing"));
app.MapContestEndpoints();
app.MapNotificationEndpoints(!app.Environment.IsEnvironment("Testing"));

app.Run();

static string ClientKey(HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier)
    ?? $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

static FixedWindowRateLimiterOptions FixedWindow(int permitLimit, TimeSpan window) => new()
{
    PermitLimit = permitLimit,
    Window = window,
    QueueLimit = 0,
    AutoReplenishment = true
};

public partial class Program;
