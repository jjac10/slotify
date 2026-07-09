using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Calcula los slots libres de un servicio para un staff y día:
/// horario del negocio − festivos − reservas existentes, en una rejilla cuyo paso
/// es configurable (businesses.slot_interval_minutes; por defecto, la duración del
/// servicio). Schema/algoritmo: docs/design/reservations-core.md (Anexo A).
///
/// NOTA: el horario del negocio se interpreta como hora local de su zona
/// (businesses.timezone, IANA) y se convierte a UTC para los slots. Esto respeta el
/// horario de verano/invierno (DST) automáticamente.
/// </summary>
public class AvailabilityService(
    IBusinessRepository businesses,
    IServiceRepository services,
    IStaffRepository staff,
    IBusinessHourRepository hours,
    IBusinessHolidayRepository holidays,
    IReservationRepository reservations)
{
    /// <param name="nowUtc">
    /// Momento actual (UTC). Si se indica, no se ofrecen slots cuyo inicio ya pasó
    /// (útil para el día de hoy). Si es null, se devuelven todos los del horario.
    /// </param>
    public async Task<IReadOnlyList<AvailableSlot>> GetSlotsAsync(
        Guid businessId, Guid serviceId, Guid staffId, DateOnly date, DateTime? nowUtc = null, CancellationToken ct = default)
    {
        var (business, service) = await LoadAndValidateAsync(businessId, serviceId, staffId, ct);

        var weeklyHours = await hours.ListByBusinessAsync(businessId, ct);
        var allHolidays = await holidays.ListByBusinessAsync(businessId, ct);
        var occupied = await reservations.ListByStaffOnDateAsync(staffId, date, ct);
        var tz = TimeZoneInfo.FindSystemTimeZoneById(business.Timezone);

        var (_, slots) = ComputeDay(business, service, tz, weeklyHours, allHolidays, occupied, date, nowUtc);
        return slots;
    }

    /// <summary>
    /// Estado de cada día de un mes para el calendario de reserva ('closed' | 'full' |
    /// 'available'). Carga horario/festivos una vez y las reservas del mes en una sola
    /// consulta (no un query por día).
    /// </summary>
    public async Task<IReadOnlyList<DayAvailability>> GetMonthAsync(
        Guid businessId, Guid serviceId, Guid staffId, int year, int month, DateTime? nowUtc = null, CancellationToken ct = default)
    {
        var (business, service) = await LoadAndValidateAsync(businessId, serviceId, staffId, ct);

        var weeklyHours = await hours.ListByBusinessAsync(businessId, ct);
        var allHolidays = await holidays.ListByBusinessAsync(businessId, ct);
        var tz = TimeZoneInfo.FindSystemTimeZoneById(business.Timezone);

        // Ventana UTC del mes local del negocio, para traer sus reservas de una vez.
        var localStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(localStart.AddMonths(1), tz);
        var occupied = await reservations.ListByStaffBetweenAsync(staffId, fromUtc, toUtc, ct);

        var days = new List<DayAvailability>(DateTime.DaysInMonth(year, month));
        for (var day = 1; day <= DateTime.DaysInMonth(year, month); day++)
        {
            var date = new DateOnly(year, month, day);
            var (isClosed, slots) = ComputeDay(business, service, tz, weeklyHours, allHolidays, occupied, date, nowUtc);
            days.Add(new DayAvailability(date, isClosed ? "closed" : slots.Count > 0 ? "available" : "full"));
        }
        return days;
    }

    private async Task<(Business Business, Service Service)> LoadAndValidateAsync(
        Guid businessId, Guid serviceId, Guid staffId, CancellationToken ct)
    {
        var business = await businesses.GetByIdAsync(businessId, ct)
            ?? throw new BusinessNotFoundException(businessId);

        var service = await services.GetByIdAsync(serviceId, ct);
        if (service is null || service.BusinessId != businessId)
            throw new ServiceNotFoundException(serviceId);

        var worker = await staff.GetByIdAsync(staffId, ct);
        if (worker is null || worker.BusinessId != businessId)
            throw new StaffNotFoundException(staffId);

        return (business, service);
    }

    /// <summary>
    /// Núcleo del cálculo de un día sobre datos ya cargados. <c>IsClosed</c> = sin
    /// horario ese día de la semana o festivo de día completo; si está abierto,
    /// <c>Slots</c> son los huecos libres (puede ser vacío = día completo).
    /// </summary>
    private static (bool IsClosed, IReadOnlyList<AvailableSlot> Slots) ComputeDay(
        Business business, Service service, TimeZoneInfo tz,
        IReadOnlyList<BusinessHour> weeklyHours, IReadOnlyList<BusinessHoliday> allHolidays,
        IReadOnlyList<Reservation> occupied, DateOnly date, DateTime? nowUtc)
    {
        // Horario del día: si está cerrado o no hay franja → cerrado.
        var dayHours = weeklyHours.FirstOrDefault(h => h.DayOfWeek == (int)date.DayOfWeek);
        if (dayHours is null || dayHours.IsClosed || dayHours.OpeningTime is null || dayHours.ClosingTime is null)
            return (true, []);

        // Festivos que cubren este día (un día suelto o un rango).
        var coveringHolidays = allHolidays
            .Where(h => h.IsClosed && h.HolidayDate <= date && date <= (h.EndDate ?? h.HolidayDate))
            .ToList();
        // Alguno cierra el día completo (sin franja horaria) → cerrado.
        if (coveringHolidays.Any(h => h.StartTime is null || h.EndTime is null))
            return (true, []);
        // Franjas horarias cerradas ese día (cierre parcial), en minutos locales.
        var closedWindows = coveringHolidays
            .Select(h => (start: h.StartTime!.Value.Hour * 60 + h.StartTime.Value.Minute,
                          end: h.EndTime!.Value.Hour * 60 + h.EndTime.Value.Minute))
            .ToList();

        var duration = service.DurationMinutes;
        var step = business.SlotIntervalMinutes ?? duration;
        if (step <= 0 || duration <= 0)
            return (false, []);

        var openMinutes = dayHours.OpeningTime.Value.Hour * 60 + dayHours.OpeningTime.Value.Minute;
        var closeMinutes = dayHours.ClosingTime.Value.Hour * 60 + dayHours.ClosingTime.Value.Minute;

        var slots = new List<AvailableSlot>();
        for (var m = openMinutes; m + duration <= closeMinutes; m += step)
        {
            // Cierre parcial por festivo: descartar slots que solapen una franja cerrada.
            if (closedWindows.Any(w => m < w.end && m + duration > w.start))
                continue;

            // El horario se interpreta como hora local del negocio (su zona IANA) y se
            // convierte a UTC. ConvertTimeToUtc respeta DST (verano/invierno) automáticamente.
            var localStart = new DateTime(date.Year, date.Month, date.Day, m / 60, m % 60, 0, DateTimeKind.Unspecified);
            var start = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
            var end = start.AddMinutes(duration);

            // No ofrecer horas que ya pasaron (p. ej. hoy a partir de la hora actual).
            if (nowUtc is { } now && start <= now)
                continue;

            var overlaps = occupied.Any(o => o.StartTime < end && o.EndTime > start);
            if (!overlaps)
                slots.Add(new AvailableSlot(start, end));
        }
        return (false, slots);
    }
}
