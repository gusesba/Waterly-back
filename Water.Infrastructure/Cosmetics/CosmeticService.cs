using Microsoft.EntityFrameworkCore;
using Water.Application.Cosmetics;
using Water.Application.Habits;
using Water.Domain.Cosmetics;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Cosmetics;

public sealed class CosmeticService(
    WaterDbContext dbContext,
    IAchievementService achievementService,
    TimeProvider timeProvider) : ICosmeticService
{
    public async Task<CharacterLoadoutResponse> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var achievements = await achievementService.GetAchievementsAsync(userId, cancellationToken);
        var unlockedAchievements = achievements.Where(item => item.IsUnlocked).Select(item => item.Code).ToHashSet();
        var auras = await dbContext.CosmeticItems.AsNoTracking().OrderBy(item => item.SortOrder).ToArrayAsync(cancellationToken);
        var ownedCodes = (await dbContext.UserCosmetics.Where(item => item.UserId == userId)
            .Select(item => item.CosmeticCode).ToArrayAsync(cancellationToken)).ToHashSet();
        var now = timeProvider.GetUtcNow();

        foreach (var aura in auras.Where(item => item.RequiredAchievementCode is null || unlockedAchievements.Contains(item.RequiredAchievementCode)))
        {
            if (!ownedCodes.Add(aura.Code)) continue;
            dbContext.UserCosmetics.Add(new UserCosmetic(userId, aura.Code, now));
        }

        var loadout = await dbContext.CharacterLoadouts.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (loadout is null)
        {
            loadout = new CharacterLoadout(userId, now);
            dbContext.CharacterLoadouts.Add(loadout);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(loadout, auras, ownedCodes);
    }

    public async Task<CharacterLoadoutResponse> UpdateAsync(string userId, string auraCode, CancellationToken cancellationToken)
    {
        await GetAsync(userId, cancellationToken);
        var isOwned = await dbContext.UserCosmetics.AnyAsync(
            item => item.UserId == userId && item.CosmeticCode == auraCode, cancellationToken);
        if (!isOwned) throw new CosmeticNotAvailableException();

        var loadout = await dbContext.CharacterLoadouts.SingleAsync(item => item.UserId == userId, cancellationToken);
        loadout.EquipAura(auraCode, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(userId, cancellationToken);
    }

    private static CharacterLoadoutResponse Map(CharacterLoadout loadout, IEnumerable<CosmeticItem> auras, IReadOnlySet<string> ownedCodes) =>
        new("axolotl-pink", loadout.AuraCode, auras.Select(item => new CosmeticResponse(
            item.Code, ownedCodes.Contains(item.Code), item.Code == loadout.AuraCode, item.RequiredAchievementCode)).ToArray());
}
