using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Water.Infrastructure.Persistence;
using Npgsql;

namespace Water.Tests.Integration;

public class WaterApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly string? _postgres = Environment.GetEnvironmentVariable("WATERLY_TEST_POSTGRES");
    private readonly string _databaseName = $"waterly_test_{Guid.NewGuid():N}";
    private bool _databaseCreated;

    private string PostgresConnection(string database) => new NpgsqlConnectionStringBuilder(_postgres) { Database = database }.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DailyClosure:Enabled"] = "false",
                ["ContestClosure:Enabled"] = "false",
                ["PushNotifications:Enabled"] = "false",
                ["RateLimits:AuthPermitLimit"] = "10000",
                ["RateLimits:SocialPermitLimit"] = "10000",
                ["RateLimits:ContestReadPermitLimit"] = "10000",
                ["RateLimits:ContestWritePermitLimit"] = "10000",
                ["Administration:ContestAdminEmails:0"] = "contest-admin@example.com"
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<WaterDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<WaterDbContext>>();
            services.RemoveAll<WaterDbContext>();
            services.RemoveAll<TimeProvider>();
            services.AddDbContext<WaterDbContext>(options =>
            {
                if (string.IsNullOrWhiteSpace(_postgres)) options.UseSqlite(_connection);
                else options.UseNpgsql(PostgresConnection(_databaseName));
            });
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(
                new DateTimeOffset(2026, 9, 15, 15, 0, 0, TimeSpan.Zero)));
        });
    }

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_postgres)) await _connection.OpenAsync();
        else
        {
            await using var connection = new NpgsqlConnection(PostgresConnection("postgres"));
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", connection);
            await command.ExecuteNonQueryAsync();
            _databaseCreated = true;
        }
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WaterDbContext>();
        if (string.IsNullOrWhiteSpace(_postgres)) await dbContext.Database.EnsureCreatedAsync();
        else await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _connection.DisposeAsync();
        if (_databaseCreated)
        {
            await using var connection = new NpgsqlConnection(PostgresConnection("postgres"));
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE \"{_databaseName}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

public sealed class RateLimitedWaterApiFactory : WaterApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment("RateLimitTesting");
    }
}
