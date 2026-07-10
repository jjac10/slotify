using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>
/// Borrado en cascada y directorio de moderación de negocios. El cascade replica el
/// del borrado de cuenta de owner: notificaciones y reservas explícitas (FK RESTRICT)
/// y el resto (staff, servicios, horarios, festivos, invitados, reseñas) por
/// ON DELETE CASCADE, todo dentro de UNA transacción.
/// </summary>
public interface IBusinessDeletionRepository
{
    /// <summary>¿El negocio tiene reservas futuras activas (pending/confirmed)?</summary>
    Task<bool> HasFutureActiveReservationsAsync(Guid businessId, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Elimina el negocio y todos sus datos en una transacción.</summary>
    Task DeleteBusinessCascadeAsync(Guid businessId, CancellationToken ct = default);

    /// <summary>
    /// Página del directorio de moderación: negocios (filtrados por nombre con
    /// <paramref name="q"/>) con el email de su owner, más recientes primero.
    /// </summary>
    Task<(IReadOnlyList<(Business Business, string OwnerEmail)> Items, int Total)> ListForAdminAsync(
        string? q, int skip, int take, CancellationToken ct = default);
}
