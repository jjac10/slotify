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

    /// <summary>Borrado físico de la reserva (hard delete, ADR #13).</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Reservas no canceladas de un staff en una fecha (para calcular disponibilidad).</summary>
    Task<IReadOnlyList<Reservation>> ListByStaffOnDateAsync(Guid staffId, DateOnly date, CancellationToken ct = default);
}
