using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

/// <summary>Implementación EF Core de <see cref="IAuditLogRepository"/>.</summary>
public class AuditLogRepository(SlotifyDbContext db) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog entry, CancellationToken ct = default)
    {
        db.AuditLogs.Add(entry);
        await db.SaveChangesAsync(ct);
    }
}
