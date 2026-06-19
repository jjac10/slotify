using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

/// <summary>Implementación EF Core de <see cref="IStaffServiceRepository"/>.</summary>
public class StaffServiceRepository(SlotifyDbContext db) : IStaffServiceRepository
{
    public async Task<IReadOnlyList<Guid>> ListServiceIdsByStaffAsync(Guid staffId, CancellationToken ct = default)
        => await db.StaffServices.AsNoTracking()
            .Where(a => a.StaffId == staffId)
            .Select(a => a.ServiceId)
            .ToListAsync(ct);

    public async Task SetForStaffAsync(Guid staffId, IReadOnlyList<Guid> serviceIds, CancellationToken ct = default)
    {
        var existing = await db.StaffServices.Where(a => a.StaffId == staffId).ToListAsync(ct);
        db.StaffServices.RemoveRange(existing);

        foreach (var serviceId in serviceIds.Distinct())
            db.StaffServices.Add(new StaffServiceAssignment { Id = Guid.NewGuid(), StaffId = staffId, ServiceId = serviceId });

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Staff>> ListStaffByServiceAsync(Guid businessId, Guid serviceId, CancellationToken ct = default)
        => await db.Staff.AsNoTracking()
            .Where(s => s.BusinessId == businessId && s.Status == "active")
            .Where(s => !db.StaffServices.Any(a => a.StaffId == s.Id)
                     || db.StaffServices.Any(a => a.StaffId == s.Id && a.ServiceId == serviceId))
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
}
