using Slotify.Domain.DTOs;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Resumen del panel del propietario de un negocio: contadores de reservas
/// (histórico y del mes en curso), ingresos estimados del mes y próximas reservas.
/// Solo el owner del negocio (404 si no existe, 403 si no es el dueño).
/// </summary>
public class DashboardService(
    IReservationRepository reservations,
    IBusinessRepository businesses)
{
    /// <summary>Nº máximo de próximas reservas incluidas en el resumen.</summary>
    public const int UpcomingLimit = 5;

    /// <param name="nowUtc">Momento de referencia (UTC) — inyectado para test determinista.</param>
    public async Task<DashboardResponse> GetAsync(
        Guid businessId, Guid currentUserId, DateTime nowUtc, CancellationToken ct = default)
    {
        var business = await businesses.GetByIdAsync(businessId, ct)
            ?? throw new BusinessNotFoundException(businessId);

        if (business.OwnerId != currentUserId)
            throw new NotBusinessOwnerException();

        var monthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var total = await reservations.CountByBusinessAsync(businessId, null, null, ct);
        var thisMonth = await reservations.CountByBusinessAsync(businessId, monthStart, monthEnd, ct);
        var revenue = await reservations.SumRevenueByBusinessAsync(businessId, monthStart, monthEnd, ct);
        var upcoming = await reservations.ListUpcomingByBusinessAsync(businessId, nowUtc, UpcomingLimit, ct);

        return new DashboardResponse(
            total,
            thisMonth,
            revenue,
            upcoming.Select(ReservationResponse.From).ToList());
    }
}
