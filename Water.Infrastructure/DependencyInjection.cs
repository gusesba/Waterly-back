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
        services.AddScoped<IHabitService, HabitService>();
        services.AddScoped<IDailyHydrationProjectionService, DailyHydrationProjectionService>();
        services.AddScoped<IAchievementService, AchievementService>();
        services.AddScoped<IProgressionService, ProgressionService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IPublicProfileService, PublicProfileService>();
        services.AddScoped<ICosmeticService, CosmeticService>();
        services.AddScoped<IDailyClosureService, DailyClosureService>();
        services.AddScoped<IFriendService, FriendService>();
        services.AddScoped<IBlockService, BlockService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IFeedService, FeedService>();
        services.AddScoped<IContestService, ContestService>();
        services.AddScoped<IContestScoreService, ContestScoreService>();
        services.AddScoped<IContestLeaderboardService, ContestLeaderboardService>();
        services.AddScoped<IContestFinalizationService, ContestFinalizationService>();
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
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        return services;
    }
}
