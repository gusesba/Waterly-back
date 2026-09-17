using Microsoft.EntityFrameworkCore;
using Water.Application.Progression;
using Water.Infrastructure.Persistence;

namespace Water.Infrastructure.Progression;

public sealed class ProgressionService(WaterDbContext dbContext) : IProgressionService
{
    public async Task<ProgressionBalanceResponse> GetDropsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var balance = await dbContext.DropsLedgerEntries
            .Where(item => item.UserId == userId)
            .SumAsync(item => (int?)item.Amount, cancellationToken) ?? 0;
        var entries = await dbContext.DropsLedgerEntries
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .ToArrayAsync(cancellationToken);
        var page = entries.OrderByDescending(item => item.CreatedAt).Take(50).ToArray();
        var labels = await ContestLabelsAsync(page.Where(item => item.ReferenceType == "contest").Select(item => item.ReferenceId), cancellationToken);
        return new ProgressionBalanceResponse(balance, page.Select(item => new ProgressionEntryResponse(
                item.Id,
                item.Amount,
                item.EntryType,
                item.ReferenceType,
                item.ReferenceId,
                labels.GetValueOrDefault(item.ReferenceId),
                item.CreatedAt))
            .ToArray());
    }

    public async Task<ProgressionBalanceResponse> GetPrestigeAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var balance = await dbContext.PrestigeLedgerEntries
            .Where(item => item.UserId == userId)
            .SumAsync(item => (int?)item.Amount, cancellationToken) ?? 0;
        var entries = await dbContext.PrestigeLedgerEntries
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .ToArrayAsync(cancellationToken);
        var page = entries.OrderByDescending(item => item.CreatedAt).Take(50).ToArray();
        var labels = await ContestLabelsAsync(page.Where(item => item.ReferenceType == "contest").Select(item => item.ReferenceId), cancellationToken);
        return new ProgressionBalanceResponse(balance, page.Select(item => new ProgressionEntryResponse(
                item.Id,
                item.Amount,
                item.EntryType,
                item.ReferenceType,
                item.ReferenceId,
                labels.GetValueOrDefault(item.ReferenceId),
                item.CreatedAt))
            .ToArray());
    }

    private async Task<Dictionary<string, string>> ContestLabelsAsync(IEnumerable<string> references, CancellationToken token)
    {
        var ids = references.Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToArray();
        return await dbContext.Contests.AsNoTracking().Where(item => ids.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id.ToString(), item => item.Name, token);
    }
}
