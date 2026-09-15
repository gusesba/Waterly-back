using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Water.Domain.Hydration;
using Water.Domain.Habits;
using Water.Domain.Profiles;
using Water.Infrastructure.Identity;

namespace Water.Infrastructure.Persistence;

public sealed class WaterDbContext(DbContextOptions<WaterDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<UserProfile> Profiles => Set<UserProfile>();
    public DbSet<HydrationGoal> HydrationGoals => Set<HydrationGoal>();
    public DbSet<DrinkEntry> DrinkEntries => Set<DrinkEntry>();
    public DbSet<Beverage> Beverages => Set<Beverage>();
    public DbSet<HydrationOperation> HydrationOperations => Set<HydrationOperation>();
    public DbSet<DailyHydration> DailyHydrations => Set<DailyHydration>();
    public DbSet<UserStreak> UserStreaks => Set<UserStreak>();
    public DbSet<AchievementDefinition> AchievementDefinitions => Set<AchievementDefinition>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<UserProfile>(profile =>
        {
            profile.HasKey(item => item.Id);
            profile.HasIndex(item => item.UserId).IsUnique();
            profile.Property(item => item.UserId).HasMaxLength(450);
            profile.Property(item => item.TimeZone).HasMaxLength(50);
            profile.Property(item => item.WeightKg).HasPrecision(5, 2);
            profile.HasOne<ApplicationUser>()
                .WithOne()
                .HasForeignKey<UserProfile>(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            profile.HasMany(item => item.Goals)
                .WithOne()
                .HasForeignKey(item => item.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProfileGoal>(goal =>
        {
            goal.HasKey(item => new { item.ProfileId, item.Goal });
            goal.Property(item => item.Goal).HasMaxLength(32);
        });

        builder.Entity<HydrationGoal>(goal =>
        {
            goal.HasKey(item => item.Id);
            goal.HasIndex(item => new { item.UserId, item.EffectiveFrom }).IsUnique();
            goal.Property(item => item.UserId).HasMaxLength(450);
            goal.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DrinkEntry>(entry =>
        {
            entry.HasKey(item => item.Id);
            entry.HasIndex(item => new { item.UserId, item.ClientEntryId }).IsUnique();
            entry.HasIndex(item => new { item.UserId, item.OccurredAtUtc });
            entry.Property(item => item.UserId).HasMaxLength(450);
            entry.Property(item => item.TimeZone).HasMaxLength(50);
            entry.Property(item => item.Source).HasMaxLength(32);
            entry.Property(item => item.BeverageCode).HasMaxLength(32);
            entry.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entry.HasOne<Beverage>()
                .WithMany()
                .HasForeignKey(item => item.BeverageCode)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Beverage>(beverage =>
        {
            beverage.HasKey(item => item.Code);
            beverage.Property(item => item.Code).HasMaxLength(32);
            beverage.Property(item => item.HydrationFactor).HasPrecision(4, 3);
            beverage.HasData(
                new { Code = "water", HydrationFactor = 1.000m, SortOrder = 1, IsActive = true },
                new { Code = "sparkling-water", HydrationFactor = 1.000m, SortOrder = 2, IsActive = true },
                new { Code = "coffee", HydrationFactor = 0.800m, SortOrder = 3, IsActive = true },
                new { Code = "tea", HydrationFactor = 0.900m, SortOrder = 4, IsActive = true });
        });

        builder.Entity<HydrationOperation>(operation =>
        {
            operation.HasKey(item => item.Id);
            operation.HasIndex(item => new { item.UserId, item.ClientOperationId }).IsUnique();
            operation.Property(item => item.UserId).HasMaxLength(450);
            operation.Property(item => item.OperationType).HasMaxLength(16);
            operation.Property(item => item.Payload).HasMaxLength(128);
            operation.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DailyHydration>(daily =>
        {
            daily.HasKey(item => item.Id);
            daily.HasIndex(item => new { item.UserId, item.LocalDate }).IsUnique();
            daily.Property(item => item.UserId).HasMaxLength(450);
            daily.Property(item => item.TimeZone).HasMaxLength(50);
            daily.Property(item => item.Progress).HasPrecision(6, 4);
            daily.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserStreak>(streak =>
        {
            streak.HasKey(item => item.Id);
            streak.HasIndex(item => item.UserId).IsUnique();
            streak.Property(item => item.UserId).HasMaxLength(450);
            streak.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AchievementDefinition>(achievement =>
        {
            achievement.HasKey(item => item.Code);
            achievement.Property(item => item.Code).HasMaxLength(32);
            achievement.Property(item => item.Criterion).HasMaxLength(32);
            achievement.HasData(
                new { Code = "first-goal", Criterion = "completed-days", Requirement = 1, SortOrder = 1, RuleVersion = 1 },
                new { Code = "streak-3", Criterion = "longest-streak", Requirement = 3, SortOrder = 2, RuleVersion = 1 },
                new { Code = "streak-7", Criterion = "longest-streak", Requirement = 7, SortOrder = 3, RuleVersion = 1 });
        });

        builder.Entity<UserAchievement>(achievement =>
        {
            achievement.HasKey(item => item.Id);
            achievement.HasIndex(item => new { item.UserId, item.AchievementCode }).IsUnique();
            achievement.Property(item => item.UserId).HasMaxLength(450);
            achievement.Property(item => item.AchievementCode).HasMaxLength(32);
            achievement.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            achievement.HasOne<AchievementDefinition>()
                .WithMany()
                .HasForeignKey(item => item.AchievementCode)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
