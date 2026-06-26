using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

/// <summary>Implementación EF Core de <see cref="IGuestRepository"/>.</summary>
public class GuestRepository(SlotifyDbContext db) : IGuestRepository
{
    public async Task AddAsync(Guest guest, CancellationToken ct = default)
    {
        db.Guests.Add(guest);
        await db.SaveChangesAsync(ct);
    }

    public Task<Guest?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Guests.FirstOrDefaultAsync(g => g.Id == id, ct);

    public async Task<IReadOnlyList<Guid>> ListIdsByUserAsync(Guid userId, CancellationToken ct = default)
        => await db.Guests.AsNoTracking()
            .Where(g => g.UserId == userId)
            .Select(g => g.Id)
            .ToListAsync(ct);

    public Task<Guest?> FindByHashAsync(Guid businessId, string? phoneHash, string? emailHash, CancellationToken ct = default)
        => db.Guests.FirstOrDefaultAsync(g =>
            g.BusinessId == businessId &&
            ((phoneHash != null && g.PhoneHash == phoneHash) ||
             (emailHash != null && g.EmailHash == emailHash)), ct);

    public async Task<IReadOnlyList<Guid>> FindIdsByContactHashAsync(string hash, CancellationToken ct = default)
        => await db.Guests.AsNoTracking()
            .Where(g => g.PhoneHash == hash || g.EmailHash == hash)
            .Select(g => g.Id)
            .ToListAsync(ct);

    public Task<int> LinkToUserByHashAsync(Guid userId, string? phoneHash, string? emailHash, CancellationToken ct = default)
        => db.Guests
            .Where(g => g.UserId == null &&
                ((phoneHash != null && g.PhoneHash == phoneHash) ||
                 (emailHash != null && g.EmailHash == emailHash)))
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.UserId, userId), ct);
}
