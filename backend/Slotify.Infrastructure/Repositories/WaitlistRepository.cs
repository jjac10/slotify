using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

public class WaitlistRepository(SlotifyDbContext db) : IWaitlistRepository
{
    public async Task AddAsync(WaitlistEntry entry, CancellationToken ct = default)
    {
        db.WaitlistEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    public Task<WaitlistEntry?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.WaitlistEntries
            .Include(e => e.Business)
            .Include(e => e.Service)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<WaitlistEntry>> ListByUserAsync(Guid userId, CancellationToken ct = default)
        => await db.WaitlistEntries.AsNoTracking()
            .Include(e => e.Business)
            .Include(e => e.Service)
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.Date).ThenBy(e => e.Position)
            .ToListAsync(ct);

    public Task<bool> ExistsForUserAsync(Guid serviceId, DateOnly date, Guid userId, CancellationToken ct = default)
        => db.WaitlistEntries.AnyAsync(
            e => e.ServiceId == serviceId && e.Date == date && e.UserId == userId, ct);

    public Task<int> CountWaitingAsync(Guid serviceId, DateOnly date, CancellationToken ct = default)
        => db.WaitlistEntries.CountAsync(
            e => e.ServiceId == serviceId && e.Date == date && e.Status == "waiting", ct);

    public Task<WaitlistEntry?> FirstWaitingAsync(Guid serviceId, DateOnly date, CancellationToken ct = default)
        => db.WaitlistEntries
            .Where(e => e.ServiceId == serviceId && e.Date == date && e.Status == "waiting")
            .OrderBy(e => e.Position)
            .FirstOrDefaultAsync(ct);

    public async Task UpdateAsync(WaitlistEntry entry, CancellationToken ct = default)
    {
        db.WaitlistEntries.Update(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        => await db.WaitlistEntries.Where(e => e.Id == id).ExecuteDeleteAsync(ct);
}
