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

    public Task<Guest?> FindByHashAsync(Guid businessId, string? phoneHash, string? emailHash, CancellationToken ct = default)
        => db.Guests.FirstOrDefaultAsync(g =>
            g.BusinessId == businessId &&
            ((phoneHash != null && g.PhoneHash == phoneHash) ||
             (emailHash != null && g.EmailHash == emailHash)), ct);
}
