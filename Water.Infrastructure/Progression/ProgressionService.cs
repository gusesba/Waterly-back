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
        return new ProgressionBalanceResponse(balance, entries
            .OrderByDescending(item => item.CreatedAt)
            .Take(50)
            .Select(item => new ProgressionEntryResponse(
                item.Id,
                item.Amount,
                item.EntryType,
                item.ReferenceType,
                item.ReferenceId,
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
        return new ProgressionBalanceResponse(balance, entries
            .OrderByDescending(item => item.CreatedAt)
            .Take(50)
            .Select(item => new ProgressionEntryResponse(
                item.Id,
                item.Amount,
                item.EntryType,
                item.ReferenceType,
                item.ReferenceId,
                item.CreatedAt))
            .ToArray());
    }
}
