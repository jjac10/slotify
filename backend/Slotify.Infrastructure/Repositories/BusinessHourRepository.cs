using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

/// <summary>Implementación EF Core de <see cref="IBusinessHourRepository"/>.</summary>
public class BusinessHourRepository(SlotifyDbContext db) : IBusinessHourRepository
{
    public async Task<IReadOnlyList<BusinessHour>> ListByBusinessAsync(Guid businessId, CancellationToken ct = default)
        => await db.BusinessHours.AsNoTracking()
            .Where(h => h.BusinessId == businessId)
            .OrderBy(h => h.DayOfWeek)
            .ToListAsync(ct);

    public async Task ReplaceForBusinessAsync(Guid businessId, IEnumerable<BusinessHour> hours, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.BusinessHours.Where(h => h.BusinessId == businessId).ExecuteDeleteAsync(ct);
        db.BusinessHours.AddRange(hours);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
