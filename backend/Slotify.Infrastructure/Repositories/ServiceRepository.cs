using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

/// <summary>Implementación EF Core de <see cref="IServiceRepository"/>.</summary>
public class ServiceRepository(SlotifyDbContext db) : IServiceRepository
{
    public async Task AddAsync(Service service, CancellationToken ct = default)
    {
        db.Services.Add(service);
        await db.SaveChangesAsync(ct);
    }

    public Task<Service?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Services.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Service>> ListByBusinessAsync(Guid businessId, CancellationToken ct = default)
        => await db.Services.AsNoTracking()
            .Where(s => s.BusinessId == businessId && s.Status == "active")
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

    public Task<int> CountByBusinessAsync(Guid businessId, CancellationToken ct = default)
        => db.Services.CountAsync(s => s.BusinessId == businessId && s.Status == "active", ct);
}
