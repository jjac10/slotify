using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>
/// Persistencia del borrado de cuenta (derecho de supresión RGPD). La operación
/// compuesta es transaccional: o se aplica todo o no se aplica nada.
/// </summary>
public interface IAccountDeletionRepository
{
    /// <summary>¿Tiene el negocio reservas futuras activas (pending/confirmed)?</summary>
    Task<bool> HasFutureActiveReservationsAsync(Guid businessId, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>
    /// Aplica el borrado en una única transacción: persiste el user ya anonimizado
    /// (tombstone), borra sus tokens (refresh/reset/verificación) y sus reseñas
    /// (recalculando la media de los negocios afectados), cancela sus reservas
    /// futuras, anonimiza sus fichas de invitado (cifrados fuera), desvincula sus
    /// filas de staff y elimina por completo los negocios indicados (owner).
    /// </summary>
    Task DeleteAccountAsync(User anonymizedUser, IReadOnlyList<Guid> businessIdsToDelete, DateTime nowUtc, CancellationToken ct = default);
}
