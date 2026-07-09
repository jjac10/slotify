using Slotify.Domain.DTOs;
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
    /// Reservas no canceladas de un staff cuyo inicio cae en [<paramref name="fromUtc"/>,
    /// <paramref name="toUtc"/>). Para la disponibilidad de un mes completo en una consulta.
    /// </summary>
    Task<IReadOnlyList<Reservation>> ListByStaffBetweenAsync(
        Guid staffId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>
    /// Reservas de un negocio (agenda), ordenadas por inicio (desempate por id → orden
    /// estable entre páginas). Filtros opcionales por día (UTC) y por trabajador.
    /// Pagina en BD (Skip/Take) y devuelve también el total que cumple el filtro.
    /// </summary>
    Task<(IReadOnlyList<Reservation> Items, int Total)> ListByBusinessAsync(
        Guid businessId, DateOnly? date, Guid? staffId, int skip, int take, CancellationToken ct = default);

    /// <summary>
    /// "Mis reservas" de un usuario registrado: las hechas con su cuenta más las de sus
    /// invitados vinculados (<paramref name="guestIds"/>), en UNA sola consulta paginada
    /// en BD (Skip/Take) con su total. <paramref name="scope"/> filtra por inicio respecto
    /// a <paramref name="nowUtc"/>: Upcoming (&gt;=, ascendente), Past (&lt;, descendente:
    /// la más reciente primero) o All (ascendente).
    /// </summary>
    Task<(IReadOnlyList<Reservation> Items, int Total)> ListByUserAsync(
        Guid userId, IReadOnlyCollection<Guid> guestIds, ReservationScope scope, DateTime nowUtc,
        int skip, int take, CancellationToken ct = default);

    /// <summary>Reservas no canceladas de unos invitados (ver "mis reservas" por teléfono/email).</summary>
    Task<IReadOnlyList<Reservation>> ListByGuestIdsAsync(IReadOnlyCollection<Guid> guestIds, CancellationToken ct = default);

    /// <summary>
    /// Cuenta las reservas no canceladas de un negocio. Ventana temporal opcional por
    /// inicio: <paramref name="fromUtc"/> inclusivo, <paramref name="toUtc"/> exclusivo
    /// (ambos null → histórico completo). Para el panel del owner.
    /// </summary>
    Task<int> CountByBusinessAsync(Guid businessId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default);

    /// <summary>
    /// Suma el precio del servicio de las reservas no canceladas de un negocio cuyo
    /// inicio cae en [<paramref name="fromUtc"/>, <paramref name="toUtc"/>). Los
    /// servicios sin precio (gratuitos) suman 0. Para los ingresos estimados del panel.
    /// </summary>
    Task<decimal> SumRevenueByBusinessAsync(Guid businessId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>
    /// Próximas reservas no canceladas de un negocio (inicio &gt;= <paramref name="fromUtc"/>),
    /// ordenadas por inicio ascendente y limitadas a <paramref name="limit"/>.
    /// </summary>
    Task<IReadOnlyList<Reservation>> ListUpcomingByBusinessAsync(Guid businessId, DateTime fromUtc, int limit, CancellationToken ct = default);

    /// <summary>
    /// Reservas no canceladas (pending/confirmed) cuyo inicio cae en (<paramref name="fromUtc"/>,
    /// <paramref name="untilUtc"/>], con su negocio cargado (<see cref="Reservation.Business"/>),
    /// para calcular los recordatorios de cita pendientes de enviar.
    /// </summary>
    Task<IReadOnlyList<Reservation>> ListUpcomingForReminderAsync(DateTime fromUtc, DateTime untilUtc, CancellationToken ct = default);
}
