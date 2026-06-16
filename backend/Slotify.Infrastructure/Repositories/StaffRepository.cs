using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

/// <summary>Implementación EF Core de <see cref="IStaffRepository"/>.</summary>
public class StaffRepository(SlotifyDbContext db) : IStaffRepository
{
    public Task<int> CountByBusinessAsync(Guid businessId, CancellationToken ct = default)
        => db.Staff.CountAsync(s => s.BusinessId == businessId, ct);

    public Task<Staff?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Staff.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<bool> ExistsForUserAsync(Guid userId, Guid businessId, CancellationToken ct = default)
        => db.Staff.AnyAsync(s => s.UserId == userId && s.BusinessId == businessId, ct);
}
