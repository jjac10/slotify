using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

/// <summary>
/// Implementación EF Core de <see cref="IAccountDeletionRepository"/>: todo el
/// borrado de cuenta ocurre dentro de UNA transacción (o se aplica todo o nada).
/// </summary>
public class AccountDeletionRepository(SlotifyDbContext db) : IAccountDeletionRepository
{
    private const string AnonymizedRecipient = "(anonimizado)";

    public Task<bool> HasFutureActiveReservationsAsync(Guid businessId, DateTime nowUtc, CancellationToken ct = default)
        => db.Reservations.AnyAsync(r => r.BusinessId == businessId
            && r.StartTime > nowUtc
            && (r.Status == "pending" || r.Status == "confirmed"), ct);

    public async Task DeleteAccountAsync(User anonymizedUser, IReadOnlyList<Guid> businessIdsToDelete, DateTime nowUtc, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var userId = anonymizedUser.Id;

        // 1. Tokens del usuario: fuera (refresh, reset de contraseña, verificación de email).
        await db.RefreshTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
        await db.PasswordResetTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
        await db.EmailVerificationTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);

        // 2. Reseñas: se borran (son opinión personal firmada; anonimizadas perderían
        //    su credibilidad) y se recalcula la media denormalizada de cada negocio
        //    afectado que no vaya a borrarse.
        var reviewedBusinessIds = await db.Reviews.Where(r => r.UserId == userId)
            .Select(r => r.BusinessId).Distinct().ToListAsync(ct);
        await db.Reviews.Where(r => r.UserId == userId).ExecuteDeleteAsync(ct);
        foreach (var businessId in reviewedBusinessIds.Except(businessIdsToDelete))
            await RecomputeBusinessRatingAsync(businessId, ct);

        // 3. Reservas futuras del usuario (como cliente): se cancelan — el negocio no
        //    debe quedarse con citas de alguien que ya no existe. Las pasadas se
        //    conservan apuntando al tombstone (la agenda no pierde histórico).
        await db.Reservations
            .Where(r => r.UserId == userId && r.StartTime > nowUtc && (r.Status == "pending" || r.Status == "confirmed"))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, "cancelled")
                .SetProperty(r => r.CancelledAt, nowUtc)
                .SetProperty(r => r.UpdatedAt, nowUtc)
                .SetProperty(r => r.Version, r => r.Version + 1), ct);

        // 4. Notificaciones registradas de sus reservas: el destinatario (email/teléfono)
        //    es dato personal → se anonimiza; el log del negocio se conserva.
        await db.Notifications
            .Where(n => db.Reservations.Any(r => r.Id == n.ReservationId && r.UserId == userId))
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Recipient, AnonymizedRecipient), ct);

        // 5. Fichas de invitado vinculadas a su cuenta: se anonimizan conservando la fila
        //    (sus reservas de invitado la referencian). Cifrados eliminados; los blind
        //    index se sustituyen por valores aleatorios (irreversibles e incolisionables,
        //    el CHECK phone_or_email y los UNIQUE parciales siguen cumpliéndose).
        var guests = await db.Guests.Where(g => g.UserId == userId).ToListAsync(ct);
        foreach (var guest in guests)
        {
            guest.Name = AccountDeletionService.AnonymizedName;
            guest.PhoneEncrypted = null;
            guest.EmailEncrypted = null;
            guest.PhoneHash = guest.PhoneHash is null ? null : RandomHash();
            guest.EmailHash = guest.EmailHash is null ? null : RandomHash();
            guest.UserId = null;
            guest.Status = "deleted";
            guest.UpdatedAt = nowUtc;
        }

        // 6. Staff: el empleado se desvincula; su ficha (gestionada por el negocio)
        //    queda como empleado sin cuenta y sin invitación pendiente.
        await db.Staff.Where(s => s.UserId == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.UserId, (Guid?)null)
                .SetProperty(x => x.InviteToken, (string?)null), ct);

        // 7. Negocios del owner: borrado completo. Primero notificaciones (sin FK) y
        //    reservas (sus FKs a services/staff son RESTRICT); el resto (staff,
        //    servicios, horarios, festivos, invitados, reseñas) cae por ON DELETE CASCADE.
        //    audit_logs sobrevive con reservation_id/actor_id a NULL (ADR #13/#14).
        foreach (var businessId in businessIdsToDelete)
        {
            await db.Notifications.Where(n => n.BusinessId == businessId).ExecuteDeleteAsync(ct);
            await db.Reservations.Where(r => r.BusinessId == businessId).ExecuteDeleteAsync(ct);
            await db.Businesses.Where(b => b.Id == businessId).ExecuteDeleteAsync(ct);
        }

        // 8. El user queda como tombstone anonimizado (lo referencian las reservas históricas).
        db.Users.Update(anonymizedUser);
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
    }

    /// <summary>Recalcula businesses.rating / review_count tras borrar las reseñas del usuario.</summary>
    private async Task RecomputeBusinessRatingAsync(Guid businessId, CancellationToken ct)
    {
        var stats = await db.Reviews.Where(r => r.BusinessId == businessId)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Average = (double?)g.Average(r => r.Rating) })
            .FirstOrDefaultAsync(ct);

        await db.Businesses.Where(b => b.Id == businessId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Rating, stats == null ? null : Math.Round(stats.Average!.Value, 2))
                .SetProperty(b => b.ReviewCount, stats == null ? 0 : stats.Count), ct);
    }

    /// <summary>64 hex aleatorios: mismo formato que un HMAC-SHA256, imposible de casar con un contacto real.</summary>
    private static string RandomHash()
        => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
}
