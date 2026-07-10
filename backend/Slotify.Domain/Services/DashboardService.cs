using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Resumen del panel del propietario de un negocio: contadores de reservas
/// (histórico y del mes en curso), ingresos estimados del mes, próximas reservas
/// y métricas avanzadas (tasa de no-shows y ocupación del mes transcurrido).
/// Solo el owner del negocio (404 si no existe, 403 si no es el dueño).
/// </summary>
public class DashboardService(
    IReservationRepository reservations,
    IBusinessRepository businesses,
    IReviewRepository reviews,
    IBusinessHourRepository hours,
    IBusinessHolidayRepository holidays,
    IStaffRepository staff)
{
    /// <summary>Nº máximo de próximas reservas incluidas en el resumen.</summary>
    public const int UpcomingLimit = 5;

    /// <summary>Nº máximo de reseñas recientes incluidas en el resumen.</summary>
    public const int RecentReviewsLimit = 5;

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

        // Reseñas: media/contador denormalizados en el negocio + las más recientes
        // (el límite ahora baja a SQL con el repo paginado).
        var (recentReviews, _) = await reviews.ListByBusinessAsync(businessId, 0, RecentReviewsLimit, ct);

        // Tasa de no asistencia del mes: no-shows sobre las citas ya pasadas del mes.
        var pastThisMonth = await reservations.CountByBusinessAsync(businessId, monthStart, nowUtc, ct);
        var noShows = await reservations.CountNoShowsByBusinessAsync(businessId, monthStart, nowUtc, ct);
        double? noShowRate = pastThisMonth > 0 ? (double)noShows / pastThisMonth : null;

        var occupancy = await ComputeOccupancyAsync(business, businessId, monthStart, nowUtc, ct);

        return new DashboardResponse(
            total,
            thisMonth,
            revenue,
            upcoming.Select(ReservationResponse.From).ToList(),
            business.Rating,
            business.ReviewCount,
            recentReviews.Select(ReviewResponse.From).ToList(),
            noShows,
            noShowRate,
            occupancy);
    }

    /// <summary>
    /// Ocupación del mes transcurrido: minutos reservados / capacidad de apertura
    /// (minutos del horario semanal por día local del negocio, × staff activo,
    /// descontando festivos de día completo). Aproximación: ignora los cierres
    /// parciales por horas. Null si no hay horario o staff (sin capacidad).
    /// </summary>
    private async Task<double?> ComputeOccupancyAsync(
        Business business, Guid businessId, DateTime monthStartUtc, DateTime nowUtc, CancellationToken ct)
    {
        var weeklyHours = await hours.ListByBusinessAsync(businessId, ct);
        if (weeklyHours.Count == 0)
            return null;

        var allHolidays = await holidays.ListByBusinessAsync(businessId, ct);
        var staffCount = await staff.CountByBusinessAsync(businessId, ct);
        if (staffCount == 0)
            return null;

        // Días locales del negocio transcurridos del mes (incluido hoy).
        var tz = TimeZoneInfo.FindSystemTimeZoneById(business.Timezone);
        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz));
        var firstOfMonth = new DateOnly(todayLocal.Year, todayLocal.Month, 1);

        var openMinutes = 0;
        for (var date = firstOfMonth; date <= todayLocal; date = date.AddDays(1))
        {
            var dayHours = weeklyHours.FirstOrDefault(h => h.DayOfWeek == (int)date.DayOfWeek);
            if (dayHours is null || dayHours.IsClosed || dayHours.OpeningTime is null || dayHours.ClosingTime is null)
                continue;
            // Festivo de día completo (suelto o rango) → sin capacidad ese día.
            if (allHolidays.Any(h => h.IsClosed && (h.StartTime is null || h.EndTime is null)
                    && h.HolidayDate <= date && date <= (h.EndDate ?? h.HolidayDate)))
                continue;

            openMinutes += (int)(dayHours.ClosingTime.Value - dayHours.OpeningTime.Value).TotalMinutes;
        }

        var capacity = openMinutes * staffCount;
        if (capacity == 0)
            return null;

        var reservedMinutes = await reservations.SumReservedMinutesAsync(businessId, monthStartUtc, nowUtc, ct);
        return Math.Min(1.0, (double)reservedMinutes / capacity);
    }
}
