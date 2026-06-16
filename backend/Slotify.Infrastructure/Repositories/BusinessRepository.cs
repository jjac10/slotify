using Microsoft.EntityFrameworkCore;
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

    public Task<Business?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Businesses.FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<IReadOnlyList<Business>> ListByOwnerAsync(Guid ownerId, CancellationToken ct = default)
        => await db.Businesses.AsNoTracking()
            .Where(b => b.OwnerId == ownerId && b.Status == "active")
            .OrderBy(b => b.Name)
            .ToListAsync(ct);
}
