using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Water.Domain.Hydration;
using Water.Domain.Profiles;
using Water.Infrastructure.Identity;

namespace Water.Infrastructure.Persistence;

public sealed class WaterDbContext(DbContextOptions<WaterDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<UserProfile> Profiles => Set<UserProfile>();
    public DbSet<HydrationGoal> HydrationGoals => Set<HydrationGoal>();

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
    }
}
