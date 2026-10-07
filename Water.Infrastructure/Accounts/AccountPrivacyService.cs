using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Water.Application.Accounts;
using Water.Infrastructure.Identity;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Accounts;

public sealed class AccountPrivacyService(
    WaterDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider) : IAccountPrivacyService
{
    public async Task<byte[]> ExportAsync(string userId, string email, CancellationToken cancellationToken)
    {
        var registeredAt = await dbContext.Users.AsNoTracking().Where(item => item.Id == userId)
            .Select(item => item.RegisteredAt).SingleAsync(cancellationToken);
        var profile = await dbContext.Profiles.AsNoTracking().Include(item => item.Goals)
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        var publicProfile = await dbContext.PublicProfiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        var hydrationGoals = await dbContext.HydrationGoals.AsNoTracking().Where(item => item.UserId == userId)
            .OrderBy(item => item.EffectiveFrom).ToArrayAsync(cancellationToken);
        var drinks = await dbContext.DrinkEntries.AsNoTracking().Where(item => item.UserId == userId)
            .OrderBy(item => item.OccurredAtUtc).ToArrayAsync(cancellationToken);
        var daily = await dbContext.DailyHydrations.AsNoTracking().Where(item => item.UserId == userId)
            .OrderBy(item => item.LocalDate).ToArrayAsync(cancellationToken);
        var achievements = await dbContext.UserAchievements.AsNoTracking().Where(item => item.UserId == userId)
            .ToArrayAsync(cancellationToken);
        var streak = await dbContext.UserStreaks.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        var drops = await dbContext.DropsLedgerEntries.AsNoTracking().Where(item => item.UserId == userId)
            .ToArrayAsync(cancellationToken);
        var prestige = await dbContext.PrestigeLedgerEntries.AsNoTracking().Where(item => item.UserId == userId)
            .ToArrayAsync(cancellationToken);
        var memberships = await (from membership in dbContext.GroupMemberships.AsNoTracking()
                                 join privateGroup in dbContext.PrivateGroups.AsNoTracking() on membership.GroupId equals privateGroup.Id
                                 where membership.UserId == userId
                                 select new { privateGroup.Id, privateGroup.Name, membership.Role, membership.JoinedAt }).ToArrayAsync(cancellationToken);
        var contestParticipation = await dbContext.ContestParticipants.AsNoTracking().Where(item => item.UserId == userId)
            .ToArrayAsync(cancellationToken);
        var contestScores = await dbContext.ContestDailyScores.AsNoTracking().Where(item => item.UserId == userId)
            .OrderBy(item => item.LocalDate).ToArrayAsync(cancellationToken);
        var contestResults = await dbContext.ContestResults.AsNoTracking().Where(item => item.UserId == userId)
            .ToArrayAsync(cancellationToken);
        var medals = await dbContext.UserMedals.AsNoTracking().Where(item => item.UserId == userId).ToArrayAsync(cancellationToken);
        var cosmetics = await dbContext.UserCosmetics.AsNoTracking().Where(item => item.UserId == userId).ToArrayAsync(cancellationToken);
        var loadout = await dbContext.CharacterLoadouts.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        var feedEvents = await dbContext.FeedEvents.AsNoTracking().Where(item => item.ActorUserId == userId).ToArrayAsync(cancellationToken);
        var feedReactions = await dbContext.FeedReactions.AsNoTracking().Where(item => item.UserId == userId).ToArrayAsync(cancellationToken);
        var contestNotificationsEnabled = await dbContext.DeviceInstallations.AsNoTracking()
            .AnyAsync(item => item.UserId == userId && item.DisabledAt == null, cancellationToken);

        var export = new
        {
            schemaVersion = 1,
            exportedAt = timeProvider.GetUtcNow(),
            account = new { email, registeredAt },
            privateProfile = profile is null ? null : new
            {
                profile.Age, profile.HeightCm, profile.WeightKg, profile.TimeZone,
                goals = profile.Goals.Select(item => item.Goal).Order().ToArray()
            },
            publicProfile = publicProfile is null ? null : new
            {
                publicProfile.Username, publicProfile.DisplayName, publicProfile.Bio
            },
            hydration = new
            {
                goals = hydrationGoals.Select(item => new { item.DailyTargetMl, item.EffectiveFrom }),
                entries = drinks.Select(item => new { item.Id, item.BeverageCode, item.VolumeMl, item.HydrationMl, item.OccurredAtUtc, item.TimeZone, item.Source, item.InputMethod }),
                daily = daily.Select(item => new { item.LocalDate, item.DailyTargetMl, item.HydrationMl, item.Progress })
            },
            achievements = achievements.Select(item => new { item.AchievementCode, item.UnlockedAt }),
            streak = streak is null ? null : new { streak.Current, streak.Longest, streak.LastCompletedDate },
            progression = new
            {
                drops = drops.OrderBy(item => item.CreatedAt).Select(item => new { item.Amount, item.ReferenceType, item.ReferenceId, item.CreatedAt }),
                prestige = prestige.OrderBy(item => item.CreatedAt).Select(item => new { item.Amount, item.ReferenceType, item.ReferenceId, item.CreatedAt })
            },
            groups = memberships,
            cosmetics = new
            {
                unlocked = cosmetics.Select(item => new { item.CosmeticCode, item.UnlockedAt }),
                equippedAura = loadout?.AuraCode
            },
            activity = new
            {
                events = feedEvents.OrderBy(item => item.CreatedAt).Select(item => new { item.EventType, item.Audience, item.ReferenceId, item.Subject, item.CreatedAt, item.GroupId }),
                reactions = feedReactions.OrderBy(item => item.CreatedAt).Select(item => new { item.FeedEventId, item.Type, item.CreatedAt })
            },
            contests = new
            {
                participation = contestParticipation.Select(item => new { item.ContestId, item.JoinedAt, item.EligibleFrom }),
                scores = contestScores.Select(item => new { item.ContestId, item.LocalDate, item.Score, item.IsFinal }),
                results = contestResults.OrderBy(item => item.FinalizedAt).Select(item => new { item.ContestId, item.Position, item.TotalScore, item.ScoredDays, item.IsTied, item.FinalizedAt }),
                medals = medals.Select(item => new { item.MedalDefinitionId, item.Position, item.AwardedAt })
            },
            notifications = new { contestNotificationsEnabled }
        };

        return JsonSerializer.SerializeToUtf8Bytes(export, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
    }

    public async Task DeleteAsync(string userId, string currentPassword, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException("Account was not found.");
        if (string.IsNullOrWhiteSpace(currentPassword) || !await userManager.CheckPasswordAsync(user, currentPassword))
            throw new InvalidAccountPasswordException();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var ownedGroups = await dbContext.PrivateGroups.Where(item => item.OwnerId == userId).ToArrayAsync(cancellationToken);
        foreach (var group in ownedGroups)
        {
            var successor = await dbContext.GroupMemberships
                .Where(item => item.GroupId == group.Id && item.UserId != userId)
                .OrderBy(item => item.JoinedAt).ThenBy(item => item.UserId)
                .FirstOrDefaultAsync(cancellationToken);
            if (successor is null) dbContext.PrivateGroups.Remove(group);
            else
            {
                group.TransferOwnership(successor.UserId, now);
                successor.PromoteToOwner();
            }
        }

        dbContext.GroupInvites.RemoveRange(dbContext.GroupInvites.Where(item => item.CreatedByUserId == userId));
        dbContext.GroupMemberships.RemoveRange(dbContext.GroupMemberships.Where(item => item.UserId == userId));
        dbContext.Friendships.RemoveRange(dbContext.Friendships.Where(item => item.UserLowId == userId || item.UserHighId == userId));
        dbContext.UserBlocks.RemoveRange(dbContext.UserBlocks.Where(item => item.BlockerUserId == userId || item.BlockedUserId == userId));
        dbContext.ContestParticipants.RemoveRange(dbContext.ContestParticipants.Where(item => item.UserId == userId));
        dbContext.ContestDailyScores.RemoveRange(dbContext.ContestDailyScores.Where(item => item.UserId == userId));
        dbContext.UserMedals.RemoveRange(dbContext.UserMedals.Where(item => item.UserId == userId));
        var finalResults = await dbContext.ContestResults.Where(item => item.UserId == userId).ToArrayAsync(cancellationToken);
        foreach (var result in finalResults) result.Anonymize();

        await dbContext.SaveChangesAsync(cancellationToken);
        var identityResult = await userManager.DeleteAsync(user);
        if (!identityResult.Succeeded)
            throw new InvalidOperationException(string.Join("; ", identityResult.Errors.Select(item => item.Description)));
        await transaction.CommitAsync(cancellationToken);
    }
}
