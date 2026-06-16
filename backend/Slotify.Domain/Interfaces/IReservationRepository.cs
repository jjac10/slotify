using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>Acceso a datos de reservas.</summary>
public interface IReservationRepository
{
    Task AddAsync(Reservation reservation, CancellationToken ct = default);

    /// <summary>
    /// ¿Hay alguna reserva no cancelada del mismo staff que solape [start, end)?
    /// (pre-check en la capa de servicio; la BD lo garantiza con exclusion constraint).
    /// </summary>
    Task<bool> HasOverlapAsync(Guid staffId, DateTime start, DateTime end, CancellationToken ct = default);

    Task<Reservation?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
