using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

/// <summary>Implementación EF Core de <see cref="IStaffRepository"/>.</summary>
public class StaffRepository(SlotifyDbContext db) : IStaffRepository
{
    public Task<int> CountByBusinessAsync(Guid businessId, CancellationToken ct = default)
        => db.Staff.CountAsync(s => s.BusinessId == businessId && s.Status == "active", ct);

    public Task<Staff?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Staff.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task AddAsync(Staff staff, CancellationToken ct = default)
    {
        db.Staff.Add(staff);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Staff staff, CancellationToken ct = default)
    {
        db.Staff.Update(staff);
        await db.SaveChangesAsync(ct);
    }

    public Task<bool> ExistsForUserAsync(Guid userId, Guid businessId, CancellationToken ct = default)
        => db.Staff.AnyAsync(s => s.UserId == userId && s.BusinessId == businessId, ct);

    public async Task<IReadOnlyList<Staff>> ListByBusinessAsync(Guid businessId, CancellationToken ct = default)
        => await db.Staff.AsNoTracking()
            .Where(s => s.BusinessId == businessId && s.Status == "active")
            .OrderByDescending(s => s.Role == "owner") // el owner siempre primero
            .ThenBy(s => s.CreatedAt)                   // el resto, por antigüedad
            .ToListAsync(ct);
}
