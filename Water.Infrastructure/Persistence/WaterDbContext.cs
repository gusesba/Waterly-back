using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Water.Domain.Hydration;
using Water.Domain.Habits;
using Water.Domain.Profiles;
using Water.Domain.Progression;
using Water.Domain.Cosmetics;
using Water.Domain.Social;
using Water.Domain.Feed;
using Water.Domain.Competition;
using Water.Domain.Notifications;
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
    public DbSet<DropsLedgerEntry> DropsLedgerEntries => Set<DropsLedgerEntry>();
    public DbSet<PrestigeLedgerEntry> PrestigeLedgerEntries => Set<PrestigeLedgerEntry>();
    public DbSet<PublicProfile> PublicProfiles => Set<PublicProfile>();
    public DbSet<CosmeticItem> CosmeticItems => Set<CosmeticItem>();
    public DbSet<UserCosmetic> UserCosmetics => Set<UserCosmetic>();
    public DbSet<CharacterLoadout> CharacterLoadouts => Set<CharacterLoadout>();
    public DbSet<DailyClosureCheckpoint> DailyClosureCheckpoints => Set<DailyClosureCheckpoint>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<PrivateGroup> PrivateGroups => Set<PrivateGroup>();
    public DbSet<GroupMembership> GroupMemberships => Set<GroupMembership>();
    public DbSet<GroupInvite> GroupInvites => Set<GroupInvite>();
    public DbSet<FeedEvent> FeedEvents => Set<FeedEvent>();
    public DbSet<FeedReaction> FeedReactions => Set<FeedReaction>();
    public DbSet<Contest> Contests => Set<Contest>();
    public DbSet<ContestParticipant> ContestParticipants => Set<ContestParticipant>();
    public DbSet<ContestDailyScore> ContestDailyScores => Set<ContestDailyScore>();
    public DbSet<ContestFinalization> ContestFinalizations => Set<ContestFinalization>();
    public DbSet<ContestResult> ContestResults => Set<ContestResult>();
    public DbSet<ContestRewardDefinition> ContestRewardDefinitions => Set<ContestRewardDefinition>();
    public DbSet<ContestRewardCheckpoint> ContestRewardCheckpoints => Set<ContestRewardCheckpoint>();
    public DbSet<MedalDefinition> MedalDefinitions => Set<MedalDefinition>();
    public DbSet<UserMedal> UserMedals => Set<UserMedal>();
    public DbSet<DeviceInstallation> DeviceInstallations => Set<DeviceInstallation>();
    public DbSet<PushNotificationMessage> PushNotificationMessages => Set<PushNotificationMessage>();

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

        builder.Entity<DailyClosureCheckpoint>(checkpoint =>
        {
            checkpoint.HasKey(item => item.Id);
            checkpoint.HasIndex(item => item.UserId).IsUnique();
            checkpoint.Property(item => item.UserId).HasMaxLength(450);
            checkpoint.HasOne<ApplicationUser>()
                .WithOne()
                .HasForeignKey<DailyClosureCheckpoint>(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AchievementDefinition>(achievement =>
        {
            achievement.HasKey(item => item.Code);
            achievement.Property(item => item.Code).HasMaxLength(32);
            achievement.Property(item => item.Criterion).HasMaxLength(32);
            achievement.HasData(
                new { Code = "first-goal", Criterion = "completed-days", Requirement = 1, SortOrder = 1, RuleVersion = 1, DropsReward = 25, PrestigeReward = 10 },
                new { Code = "streak-3", Criterion = "longest-streak", Requirement = 3, SortOrder = 2, RuleVersion = 1, DropsReward = 50, PrestigeReward = 25 },
                new { Code = "streak-7", Criterion = "longest-streak", Requirement = 7, SortOrder = 3, RuleVersion = 1, DropsReward = 100, PrestigeReward = 50 });
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

        ConfigureLedger<DropsLedgerEntry>(builder);
        ConfigureLedger<PrestigeLedgerEntry>(builder);

        builder.Entity<PublicProfile>(profile =>
        {
            profile.HasKey(item => item.Id);
            profile.HasIndex(item => item.UserId).IsUnique();
            profile.HasIndex(item => item.NormalizedUsername).IsUnique();
            profile.Property(item => item.UserId).HasMaxLength(450);
            profile.Property(item => item.Username).HasMaxLength(20);
            profile.Property(item => item.NormalizedUsername).HasMaxLength(20);
            profile.Property(item => item.DisplayName).HasMaxLength(40);
            profile.Property(item => item.Bio).HasMaxLength(160);
            profile.HasOne<ApplicationUser>().WithOne().HasForeignKey<PublicProfile>(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Friendship>(friendship =>
        {
            friendship.HasKey(item => item.Id);
            friendship.HasIndex(item => new { item.UserLowId, item.UserHighId }).IsUnique();
            friendship.Property(item => item.UserLowId).HasMaxLength(450);
            friendship.Property(item => item.UserHighId).HasMaxLength(450);
            friendship.Property(item => item.RequestedByUserId).HasMaxLength(450);
            friendship.Ignore(item => item.IsAccepted);
            friendship.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserLowId).OnDelete(DeleteBehavior.Restrict);
            friendship.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserHighId).OnDelete(DeleteBehavior.Restrict);
            friendship.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UserBlock>(block =>
        {
            block.HasKey(item => new { item.BlockerUserId, item.BlockedUserId });
            block.HasIndex(item => item.BlockedUserId);
            block.Property(item => item.BlockerUserId).HasMaxLength(450);
            block.Property(item => item.BlockedUserId).HasMaxLength(450);
            block.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.BlockerUserId).OnDelete(DeleteBehavior.Restrict);
            block.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.BlockedUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PrivateGroup>(group =>
        {
            group.HasKey(item => item.Id);
            group.Property(item => item.OwnerId).HasMaxLength(450);
            group.Property(item => item.Name).HasMaxLength(40);
            group.Property(item => item.Description).HasMaxLength(160);
            group.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GroupMembership>(membership =>
        {
            membership.HasKey(item => item.Id);
            membership.HasIndex(item => new { item.GroupId, item.UserId }).IsUnique();
            membership.Property(item => item.UserId).HasMaxLength(450);
            membership.Property(item => item.Role).HasMaxLength(16);
            membership.HasOne<PrivateGroup>().WithMany().HasForeignKey(item => item.GroupId).OnDelete(DeleteBehavior.Cascade);
            membership.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<GroupInvite>(invite =>
        {
            invite.HasKey(item => item.Id);
            invite.HasIndex(item => item.TokenHash).IsUnique();
            invite.HasIndex(item => item.GroupId);
            invite.Property(item => item.CreatedByUserId).HasMaxLength(450);
            invite.Property(item => item.TokenHash).HasMaxLength(64);
            invite.HasOne<PrivateGroup>().WithMany().HasForeignKey(item => item.GroupId).OnDelete(DeleteBehavior.Cascade);
            invite.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FeedEvent>(feedEvent =>
        {
            feedEvent.HasKey(item => item.Id);
            feedEvent.HasIndex(item => item.IdempotencyKey).IsUnique();
            feedEvent.HasIndex(item => item.SortKey);
            feedEvent.HasIndex(item => new { item.ActorUserId, item.SortKey });
            feedEvent.HasIndex(item => new { item.GroupId, item.SortKey });
            feedEvent.Property(item => item.ActorUserId).HasMaxLength(450);
            feedEvent.Property(item => item.EventType).HasMaxLength(32);
            feedEvent.Property(item => item.Audience).HasMaxLength(16);
            feedEvent.Property(item => item.ReferenceId).HasMaxLength(64);
            feedEvent.Property(item => item.Subject).HasMaxLength(80);
            feedEvent.Property(item => item.IdempotencyKey).HasMaxLength(160);
            feedEvent.Property(item => item.SortKey).HasMaxLength(46);
            feedEvent.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Cascade);
            feedEvent.HasOne<PrivateGroup>().WithMany().HasForeignKey(item => item.GroupId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FeedReaction>(reaction =>
        {
            reaction.HasKey(item => new { item.FeedEventId, item.UserId });
            reaction.HasIndex(item => new { item.FeedEventId, item.Type });
            reaction.Property(item => item.UserId).HasMaxLength(450);
            reaction.Property(item => item.Type).HasMaxLength(16);
            reaction.HasOne<FeedEvent>().WithMany().HasForeignKey(item => item.FeedEventId).OnDelete(DeleteBehavior.Cascade);
            reaction.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DeviceInstallation>(installation =>
        {
            installation.HasKey(item => item.Id);
            installation.HasIndex(item => item.UserId);
            installation.HasIndex(item => item.ExpoPushToken);
            installation.Property(item => item.UserId).HasMaxLength(450);
            installation.Property(item => item.ExpoPushToken).HasMaxLength(256);
            installation.Property(item => item.Platform).HasMaxLength(16);
            installation.Property(item => item.Locale).HasMaxLength(16);
            installation.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PushNotificationMessage>(message =>
        {
            message.HasKey(item => item.Id);
            message.HasIndex(item => new { item.DeviceInstallationId, item.IdempotencyKey }).IsUnique();
            message.HasIndex(item => new { item.CompletedAt, item.NextAttemptAt });
            message.Property(item => item.EventType).HasMaxLength(32);
            message.Property(item => item.ReferenceId).HasMaxLength(64);
            message.Property(item => item.Title).HasMaxLength(80);
            message.Property(item => item.Body).HasMaxLength(240);
            message.Property(item => item.DataJson).HasMaxLength(512);
            message.Property(item => item.IdempotencyKey).HasMaxLength(160);
            message.Property(item => item.TicketId).HasMaxLength(128);
            message.Property(item => item.LastError).HasMaxLength(160);
            message.HasOne(item => item.DeviceInstallation).WithMany().HasForeignKey(item => item.DeviceInstallationId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Contest>(contest =>
        {
            contest.HasKey(item => item.Id);
            contest.HasIndex(item => item.StartsOn);
            contest.Property(item => item.Name).HasMaxLength(50);
            contest.Property(item => item.RewardRuleVersion).HasDefaultValue(1);
        });

        builder.Entity<ContestRewardDefinition>(reward =>
        {
            reward.HasKey(item => item.Id);
            reward.HasIndex(item => new { item.RuleVersion, item.DurationDays, item.Position }).IsUnique();
            reward.HasData(
                new { Id = 1, RuleVersion = 1, DurationDays = 7, Position = 0, DropsReward = 25, PrestigeReward = 10 },
                new { Id = 2, RuleVersion = 1, DurationDays = 7, Position = 1, DropsReward = 100, PrestigeReward = 50 },
                new { Id = 3, RuleVersion = 1, DurationDays = 7, Position = 2, DropsReward = 60, PrestigeReward = 30 },
                new { Id = 4, RuleVersion = 1, DurationDays = 7, Position = 3, DropsReward = 40, PrestigeReward = 20 },
                new { Id = 5, RuleVersion = 1, DurationDays = 30, Position = 0, DropsReward = 75, PrestigeReward = 30 },
                new { Id = 6, RuleVersion = 1, DurationDays = 30, Position = 1, DropsReward = 100, PrestigeReward = 50 },
                new { Id = 7, RuleVersion = 1, DurationDays = 30, Position = 2, DropsReward = 60, PrestigeReward = 30 },
                new { Id = 8, RuleVersion = 1, DurationDays = 30, Position = 3, DropsReward = 40, PrestigeReward = 20 });
        });

        builder.Entity<ContestParticipant>(participant =>
        {
            participant.HasKey(item => item.Id);
            participant.HasIndex(item => new { item.ContestId, item.UserId }).IsUnique();
            participant.HasIndex(item => new { item.UserId, item.ClientOperationId }).IsUnique();
            participant.Property(item => item.UserId).HasMaxLength(450);
            participant.HasOne<Contest>().WithMany().HasForeignKey(item => item.ContestId).OnDelete(DeleteBehavior.Cascade);
            participant.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ContestDailyScore>(score =>
        {
            score.HasKey(item => item.Id);
            score.HasIndex(item => new { item.ContestId, item.UserId, item.LocalDate }).IsUnique();
            score.Property(item => item.UserId).HasMaxLength(450);
            score.Property(item => item.Score).HasPrecision(6, 2);
            score.HasOne<Contest>().WithMany().HasForeignKey(item => item.ContestId).OnDelete(DeleteBehavior.Cascade);
            score.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ContestFinalization>(finalization =>
        {
            finalization.HasKey(item => item.Id);
            finalization.HasIndex(item => item.ContestId).IsUnique();
            finalization.HasOne<Contest>().WithOne().HasForeignKey<ContestFinalization>(item => item.ContestId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ContestResult>(result =>
        {
            result.HasKey(item => item.Id);
            result.HasIndex(item => new { item.ContestId, item.UserId }).IsUnique();
            result.HasIndex(item => new { item.ContestId, item.Position, item.TotalScore });
            result.Property(item => item.UserId).HasMaxLength(450);
            result.Property(item => item.TotalScore).HasPrecision(8, 2);
            result.Property(item => item.Username).HasMaxLength(20);
            result.Property(item => item.DisplayName).HasMaxLength(40);
            result.HasOne<Contest>().WithMany().HasForeignKey(item => item.ContestId).OnDelete(DeleteBehavior.Cascade);
            result.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ContestRewardCheckpoint>(checkpoint =>
        {
            checkpoint.HasKey(item => item.Id);
            checkpoint.HasIndex(item => item.ContestId).IsUnique();
            checkpoint.HasOne<Contest>().WithOne().HasForeignKey<ContestRewardCheckpoint>(item => item.ContestId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MedalDefinition>(medal =>
        {
            medal.HasKey(item => item.Id);
            medal.HasIndex(item => item.ContestId).IsUnique();
            medal.HasIndex(item => item.Code).IsUnique();
            medal.Property(item => item.Code).HasMaxLength(48);
            medal.Property(item => item.Name).HasMaxLength(50);
            medal.HasOne<Contest>().WithOne().HasForeignKey<MedalDefinition>(item => item.ContestId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserMedal>(medal =>
        {
            medal.HasKey(item => item.Id);
            medal.HasIndex(item => new { item.UserId, item.MedalDefinitionId }).IsUnique();
            medal.Property(item => item.UserId).HasMaxLength(450);
            medal.HasOne<MedalDefinition>().WithMany().HasForeignKey(item => item.MedalDefinitionId).OnDelete(DeleteBehavior.Cascade);
            medal.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CosmeticItem>(cosmetic =>
        {
            cosmetic.HasKey(item => item.Code);
            cosmetic.Property(item => item.Code).HasMaxLength(32);
            cosmetic.Property(item => item.RequiredAchievementCode).HasMaxLength(32);
            cosmetic.HasOne<AchievementDefinition>()
                .WithMany()
                .HasForeignKey(item => item.RequiredAchievementCode)
                .OnDelete(DeleteBehavior.Restrict);
            cosmetic.HasData(
                new { Code = "natural", RequiredAchievementCode = (string?)null, SortOrder = 1 },
                new { Code = "ocean", RequiredAchievementCode = "first-goal", SortOrder = 2 },
                new { Code = "sunset", RequiredAchievementCode = "streak-3", SortOrder = 3 },
                new { Code = "stellar", RequiredAchievementCode = "streak-7", SortOrder = 4 });
        });

        builder.Entity<UserCosmetic>(cosmetic =>
        {
            cosmetic.HasKey(item => item.Id);
            cosmetic.HasIndex(item => new { item.UserId, item.CosmeticCode }).IsUnique();
            cosmetic.Property(item => item.UserId).HasMaxLength(450);
            cosmetic.Property(item => item.CosmeticCode).HasMaxLength(32);
            cosmetic.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
            cosmetic.HasOne<CosmeticItem>().WithMany().HasForeignKey(item => item.CosmeticCode).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CharacterLoadout>(loadout =>
        {
            loadout.HasKey(item => item.Id);
            loadout.HasIndex(item => item.UserId).IsUnique();
            loadout.Property(item => item.UserId).HasMaxLength(450);
            loadout.Property(item => item.AuraCode).HasMaxLength(32);
            loadout.HasOne<ApplicationUser>().WithOne().HasForeignKey<CharacterLoadout>(item => item.UserId).OnDelete(DeleteBehavior.Cascade);
            loadout.HasOne<CosmeticItem>().WithMany().HasForeignKey(item => item.AuraCode).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLedger<TEntry>(ModelBuilder builder) where TEntry : class
    {
        var ledger = builder.Entity<TEntry>();
        ledger.HasKey("Id");
        ledger.HasIndex("UserId", "IdempotencyKey").IsUnique();
        ledger.Property("UserId").HasMaxLength(450);
        ledger.Property("EntryType").HasMaxLength(32);
        ledger.Property("ReferenceType").HasMaxLength(32);
        ledger.Property("ReferenceId").HasMaxLength(64);
        ledger.Property("IdempotencyKey").HasMaxLength(128);
        ledger.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey("UserId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
