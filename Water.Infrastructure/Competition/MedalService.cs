using Microsoft.EntityFrameworkCore;
using Water.Application.Competition;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Competition;

public sealed class MedalService(WaterDbContext dbContext) : IMedalService
{
    public async Task<IReadOnlyCollection<MedalResponse>> GetAsync(string userId, CancellationToken token)
    {
        var medals = await (from userMedal in dbContext.UserMedals.AsNoTracking()
                            join definition in dbContext.MedalDefinitions.AsNoTracking() on userMedal.MedalDefinitionId equals definition.Id
                            where userMedal.UserId == userId
                            select new MedalResponse(
                                userMedal.Id,
                                definition.ContestId,
                                definition.Code,
                                definition.Name,
                                definition.StartsOn,
                                definition.EndsOn,
                                userMedal.Position,
                                userMedal.AwardedAt))
            .ToArrayAsync(token);
        return medals.OrderByDescending(item => item.AwardedAt).ToArray();
    }
}
