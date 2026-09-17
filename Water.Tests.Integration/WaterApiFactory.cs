using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Water.Infrastructure.Persistence;

namespace Water.Tests.Integration;

public sealed class WaterApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DailyClosure:Enabled"] = "false",
                ["Administration:ContestAdminEmails:0"] = "contest-admin@example.com"
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<WaterDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<WaterDbContext>>();
            services.RemoveAll<WaterDbContext>();
            services.RemoveAll<TimeProvider>();
            services.AddDbContext<WaterDbContext>(options => options.UseSqlite(_connection));
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(
                new DateTimeOffset(2026, 9, 15, 15, 0, 0, TimeSpan.Zero)));
        });
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WaterDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
