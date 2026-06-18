using Slotify.Domain.DTOs;
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
        var business = await businesses.GetByIdAsync(businessId, ct)
            ?? throw new BusinessNotFoundException(businessId);

        var service = await services.GetByIdAsync(serviceId, ct);
        if (service is null || service.BusinessId != businessId)
            throw new ServiceNotFoundException(serviceId);

        var worker = await staff.GetByIdAsync(staffId, ct);
        if (worker is null || worker.BusinessId != businessId)
            throw new StaffNotFoundException(staffId);

        // Horario del día: si está cerrado o no hay franja → sin slots.
        var dayHours = (await hours.ListByBusinessAsync(businessId, ct))
            .FirstOrDefault(h => h.DayOfWeek == (int)date.DayOfWeek);
        if (dayHours is null || dayHours.IsClosed || dayHours.OpeningTime is null || dayHours.ClosingTime is null)
            return [];

        // Festivo cerrado ese día → sin slots.
        var dayHolidays = await holidays.ListByBusinessAsync(businessId, ct);
        if (dayHolidays.Any(h => h.HolidayDate == date && h.IsClosed))
            return [];

        var duration = service.DurationMinutes;
        var step = business.SlotIntervalMinutes ?? duration;
        if (step <= 0 || duration <= 0)
            return [];

        var openMinutes = dayHours.OpeningTime.Value.Hour * 60 + dayHours.OpeningTime.Value.Minute;
        var closeMinutes = dayHours.ClosingTime.Value.Hour * 60 + dayHours.ClosingTime.Value.Minute;

        var occupied = await reservations.ListByStaffOnDateAsync(staffId, date, ct);

        // El horario se interpreta como hora local del negocio (su zona IANA) y se
        // convierte a UTC. ConvertTimeToUtc respeta DST (verano/invierno) automáticamente.
        var tz = TimeZoneInfo.FindSystemTimeZoneById(business.Timezone);

        var slots = new List<AvailableSlot>();
        for (var m = openMinutes; m + duration <= closeMinutes; m += step)
        {
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
        return slots;
    }
}
