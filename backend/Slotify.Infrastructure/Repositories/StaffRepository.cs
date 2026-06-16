using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

/// <summary>Implementación EF Core de <see cref="IStaffRepository"/>.</summary>
public class StaffRepository(SlotifyDbContext db) : IStaffRepository
{
    public Task<int> CountByBusinessAsync(Guid businessId, CancellationToken ct = default)
        => db.Staff.CountAsync(s => s.BusinessId == businessId, ct);
}
