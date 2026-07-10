using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

/// <summary>
/// Implementación EF Core de <see cref="IBusinessDeletionRepository"/>. El cascade
/// replica el paso 7 del borrado de cuenta (<see cref="AccountDeletionRepository"/>):
/// notificaciones y reservas explícitas (FK RESTRICT) y el resto por ON DELETE CASCADE;
/// audit_logs sobrevive con reservation_id/actor_id a NULL (ADR #13/#14).
/// </summary>
public class BusinessDeletionRepository(SlotifyDbContext db) : IBusinessDeletionRepository
{
    public Task<bool> HasFutureActiveReservationsAsync(Guid businessId, DateTime nowUtc, CancellationToken ct = default)
        => db.Reservations.AnyAsync(r => r.BusinessId == businessId
            && r.StartTime > nowUtc
            && (r.Status == "pending" || r.Status == "confirmed"), ct);

    public async Task DeleteBusinessCascadeAsync(Guid businessId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await db.Notifications.Where(n => n.BusinessId == businessId).ExecuteDeleteAsync(ct);
        await db.Reservations.Where(r => r.BusinessId == businessId).ExecuteDeleteAsync(ct);
        await db.Businesses.Where(b => b.Id == businessId).ExecuteDeleteAsync(ct);

        await tx.CommitAsync(ct);
    }

    public async Task<(IReadOnlyList<(Business Business, string OwnerEmail)> Items, int Total)> ListForAdminAsync(
        string? q, int skip, int take, CancellationToken ct = default)
    {
        var query = db.Businesses.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{q.Trim()}%";
            query = query.Where(b => EF.Functions.ILike(b.Name, pattern));
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.Id)
            .Skip(skip).Take(take)
            .Join(db.Users, b => b.OwnerId, u => u.Id, (b, u) => new { Business = b, OwnerEmail = u.Email })
            .ToListAsync(ct);

        return (rows.Select(r => (r.Business, r.OwnerEmail)).ToList(), total);
    }
}
