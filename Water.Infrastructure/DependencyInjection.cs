using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Water.Application.Profiles;
using Water.Application.Hydration;
using Water.Application.Habits;
using Water.Infrastructure.Habits;
using Water.Infrastructure.Hydration;
using Water.Infrastructure.Identity;
using Water.Infrastructure.Persistence;
using Water.Infrastructure.Profiles;
using Water.Application.Progression;
using Water.Infrastructure.Progression;
using Water.Application.Cosmetics;
using Water.Infrastructure.Cosmetics;
using Water.Application.Social;
using Water.Infrastructure.Social;
using Water.Application.Feed;
using Water.Infrastructure.Feed;
using Water.Application.Competition;
using Water.Infrastructure.Competition;
using Water.Application.Notifications;
using Water.Infrastructure.Notifications;
using Water.Application.Accounts;
using Water.Infrastructure.Accounts;
using Water.Application.Analytics;
using Water.Infrastructure.Analytics;

namespace Water.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Water")
            ?? throw new InvalidOperationException("Connection string 'Water' was not configured.");

        services.AddDbContext<WaterDbContext>(options => options.UseNpgsql(connectionString));
        services.AddIdentityApiEndpoints<ApplicationUser>(options =>
        {
            options.SignIn.RequireConfirmedEmail = false;
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
        }).AddEntityFrameworkStores<WaterDbContext>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IHydrationService, HydrationService>();
        services.AddScoped<IHydrationGoalService, HydrationGoalService>();
        services.AddScoped<IHabitService, HabitService>();
        services.AddScoped<IDailyHydrationProjectionService, DailyHydrationProjectionService>();
        services.AddScoped<IAchievementService, AchievementService>();
        services.AddScoped<IProgressionService, ProgressionService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IAccountPrivacyService, AccountPrivacyService>();
        services.AddScoped<IProductMetricsService, ProductMetricsService>();
        services.AddScoped<IPublicProfileService, PublicProfileService>();
        services.AddScoped<ICosmeticService, CosmeticService>();
        services.AddScoped<IDailyClosureService, DailyClosureService>();
        services.AddScoped<IFriendService, FriendService>();
        services.AddScoped<IBlockService, BlockService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IGroupLeaderboardService, GroupLeaderboardService>();
        services.AddScoped<IFeedService, FeedService>();
        services.AddScoped<IContestService, ContestService>();
        services.AddScoped<IContestScoreService, ContestScoreService>();
        services.AddScoped<IContestLeaderboardService, ContestLeaderboardService>();
        services.AddScoped<IContestFinalizationService, ContestFinalizationService>();
        services.AddScoped<IContestRewardService, ContestRewardService>();
        services.AddScoped<IMedalService, MedalService>();
        services.AddScoped<IDeviceInstallationService, DeviceInstallationService>();
        services.AddScoped<IContestNotificationOutbox, ContestNotificationOutbox>();
        services.AddScoped<PushNotificationProcessor>();
        services.AddHttpClient<IExpoPushGateway, ExpoPushGateway>(client => client.BaseAddress = new Uri("https://exp.host/"));
        services.AddOptions<DailyClosureOptions>()
            .Bind(configuration.GetSection(DailyClosureOptions.SectionName))
            .Validate(options => options.Interval > TimeSpan.Zero, "DailyClosure interval must be positive.")
            .Validate(options => options.BatchSize is > 0 and <= 1000, "DailyClosure batch size must be between 1 and 1000.")
            .ValidateOnStart();
        services.AddHostedService<DailyClosureWorker>();
        services.AddOptions<ContestClosureOptions>()
            .Bind(configuration.GetSection(ContestClosureOptions.SectionName))
            .Validate(options => options.Interval > TimeSpan.Zero, "ContestClosure interval must be positive.")
            .Validate(options => options.BatchSize is > 0 and <= 100, "ContestClosure batch size must be between 1 and 100.")
            .ValidateOnStart();
        services.AddHostedService<ContestClosureWorker>();
        services.AddOptions<PushNotificationOptions>()
            .Bind(configuration.GetSection(PushNotificationOptions.SectionName))
            .Validate(options => options.Interval > TimeSpan.Zero, "PushNotifications interval must be positive.")
            .Validate(options => options.BatchSize is > 0 and <= 100, "PushNotifications batch size must be between 1 and 100.")
            .Validate(options => options.MaxAttempts is > 0 and <= 20, "PushNotifications max attempts must be between 1 and 20.")
            .Validate(options => options.ReceiptDelay > TimeSpan.Zero, "PushNotifications receipt delay must be positive.")
            .ValidateOnStart();
        services.AddHostedService<PushNotificationWorker>();
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        return services;
    }
}
