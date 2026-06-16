using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>Acceso a datos de reservas.</summary>
public interface IReservationRepository
{
    Task AddAsync(Reservation reservation, CancellationToken ct = default);

    /// <summary>
    /// ¿Hay alguna reserva no cancelada del mismo staff que solape [start, end)?
    /// (pre-check en la capa de servicio; la BD lo garantiza con exclusion constraint).
    /// <paramref name="excludeReservationId"/> permite ignorar una reserva (útil al
    /// reprogramarla, para que no solape consigo misma).
    /// </summary>
    Task<bool> HasOverlapAsync(Guid staffId, DateTime start, DateTime end,
        Guid? excludeReservationId = null, CancellationToken ct = default);

    Task<Reservation?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Persiste cambios de una reserva ya cargada (reprogramar). Garantiza el
    /// anti-doble-booking (exclusion constraint → <see cref="Exceptions.SlotUnavailableException"/>)
    /// y el optimistic locking (version → <see cref="Exceptions.ReservationConcurrencyException"/>).
    /// </summary>
    Task UpdateAsync(Reservation reservation, CancellationToken ct = default);

    /// <summary>Borrado físico de la reserva (hard delete, ADR #13).</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Reservas no canceladas de un staff en una fecha (para calcular disponibilidad).</summary>
    Task<IReadOnlyList<Reservation>> ListByStaffOnDateAsync(Guid staffId, DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// Reservas de un negocio (agenda), ordenadas por inicio. Filtros opcionales por
    /// día (UTC) y por trabajador.
    /// </summary>
    Task<IReadOnlyList<Reservation>> ListByBusinessAsync(
        Guid businessId, DateOnly? date, Guid? staffId, CancellationToken ct = default);

    /// <summary>Reservas de un usuario registrado ("mis reservas"), ordenadas por inicio.</summary>
    Task<IReadOnlyList<Reservation>> ListByUserAsync(Guid userId, CancellationToken ct = default);
}
