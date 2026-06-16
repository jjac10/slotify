using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

/// <summary>Implementación EF Core de <see cref="IBusinessRepository"/>.</summary>
public class BusinessRepository(SlotifyDbContext db) : IBusinessRepository
{
    public async Task AddWithOwnerStaffAsync(Business business, Staff ownerStaff, CancellationToken ct = default)
    {
        db.Businesses.Add(business);
        db.Staff.Add(ownerStaff);
        // Un único SaveChanges => EF lo envuelve en una transacción: atómico.
        await db.SaveChangesAsync(ct);
    }
}
