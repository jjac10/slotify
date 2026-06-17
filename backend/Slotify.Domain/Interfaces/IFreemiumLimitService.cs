namespace Slotify.Domain.Interfaces;

/// <summary>Validación de límites del plan (Freemium), data-driven (ADR #9).</summary>
public interface IFreemiumLimitService
{
    Task<bool> CanAddStaffAsync(Guid businessId, CancellationToken ct = default);
    Task<bool> CanAddServiceAsync(Guid businessId, CancellationToken ct = default);

    /// <summary>
    /// ¿Puede el negocio crear una reserva más en el mes natural de <paramref name="nowUtc"/>
    /// según su plan? Ventana [primer día del mes 00:00 UTC, primer día del mes siguiente).
    /// NULL en el límite = ilimitado.
    /// </summary>
    Task<bool> CanAddReservationThisMonthAsync(Guid businessId, DateTime nowUtc, CancellationToken ct = default);
}
