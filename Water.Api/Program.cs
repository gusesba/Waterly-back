using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
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
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
    options.AddPolicy("social", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}
app.UseRateLimiter();
app.UseCors();
app.UseAuthentication();
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
app.MapContestEndpoints(!app.Environment.IsEnvironment("Testing"));
app.MapNotificationEndpoints(!app.Environment.IsEnvironment("Testing"));

app.Run();

public partial class Program;
