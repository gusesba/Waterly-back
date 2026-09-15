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
        services.AddScoped<IAchievementService, AchievementService>();
        services.AddScoped<IProgressionService, ProgressionService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        return services;
    }
}
