using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

public class GuestOtpRepository(SlotifyDbContext db) : IGuestOtpRepository
{
    public async Task AddAsync(GuestOtpCode code, CancellationToken ct = default)
    {
        db.GuestOtpCodes.Add(code);
        await db.SaveChangesAsync(ct);
    }

    public async Task<GuestOtpCode?> GetLatestByContactHashAsync(string contactHash, CancellationToken ct = default)
    {
        return await db.GuestOtpCodes
            .Where(c => c.ContactHash == contactHash)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpdateAsync(GuestOtpCode code, CancellationToken ct = default)
    {
        db.GuestOtpCodes.Update(code);
        await db.SaveChangesAsync(ct);
    }
}
