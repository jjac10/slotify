using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

/// <summary>Implementación EF Core de <see cref="IBusinessHolidayRepository"/>.</summary>
public class BusinessHolidayRepository(SlotifyDbContext db) : IBusinessHolidayRepository
{
    public async Task AddAsync(BusinessHoliday holiday, CancellationToken ct = default)
    {
        db.BusinessHolidays.Add(holiday);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<BusinessHoliday>> ListByBusinessAsync(Guid businessId, CancellationToken ct = default)
        => await db.BusinessHolidays.AsNoTracking()
            .Where(h => h.BusinessId == businessId)
            .OrderBy(h => h.HolidayDate)
            .ToListAsync(ct);

    public Task<BusinessHoliday?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.BusinessHolidays.FirstOrDefaultAsync(h => h.Id == id, ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        => await db.BusinessHolidays.Where(h => h.Id == id).ExecuteDeleteAsync(ct);
}
