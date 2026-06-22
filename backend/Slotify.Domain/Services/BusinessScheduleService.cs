using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Gestión del horario de un negocio (horario semanal + festivos). Mutaciones
/// solo del owner; lectura pública. Validación de días y franjas.
/// </summary>
public class BusinessScheduleService(
    IBusinessRepository businesses,
    IBusinessHourRepository hours,
    IBusinessHolidayRepository holidays)
{
    public async Task SetHoursAsync(
        Guid businessId, Guid userId, IReadOnlyList<BusinessHourInput> days, CancellationToken ct = default)
    {
        await EnsureOwnerAsync(businessId, userId, ct);
        Validate(days);

        var entities = days.Select(d => new BusinessHour
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            DayOfWeek = d.DayOfWeek,
            IsClosed = d.IsClosed,
            OpeningTime = d.IsClosed ? null : d.OpeningTime,
            ClosingTime = d.IsClosed ? null : d.ClosingTime,
        });
        await hours.ReplaceForBusinessAsync(businessId, entities, ct);
    }

    public async Task<IReadOnlyList<BusinessHourResponse>> GetHoursAsync(Guid businessId, CancellationToken ct = default)
        => (await hours.ListByBusinessAsync(businessId, ct)).Select(BusinessHourResponse.From).ToList();

    public async Task<BusinessHolidayResponse> AddHolidayAsync(
        Guid businessId, Guid userId, CreateHolidayRequest request, CancellationToken ct = default)
    {
        await EnsureOwnerAsync(businessId, userId, ct);
        ValidateHoliday(request);

        var holiday = new BusinessHoliday
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            HolidayDate = request.HolidayDate,
            EndDate = request.EndDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Reason = request.Reason,
            IsClosed = request.IsClosed,
        };
        await holidays.AddAsync(holiday, ct);
        return BusinessHolidayResponse.From(holiday);
    }

    public async Task<IReadOnlyList<BusinessHolidayResponse>> ListHolidaysAsync(Guid businessId, CancellationToken ct = default)
        => (await holidays.ListByBusinessAsync(businessId, ct)).Select(BusinessHolidayResponse.From).ToList();

    public async Task DeleteHolidayAsync(Guid holidayId, Guid userId, CancellationToken ct = default)
    {
        var holiday = await holidays.GetByIdAsync(holidayId, ct)
            ?? throw new HolidayNotFoundException(holidayId);
        await EnsureOwnerAsync(holiday.BusinessId, userId, ct);
        await holidays.DeleteAsync(holidayId, ct);
    }

    private async Task EnsureOwnerAsync(Guid businessId, Guid userId, CancellationToken ct)
    {
        var business = await businesses.GetByIdAsync(businessId, ct)
            ?? throw new BusinessNotFoundException(businessId);
        if (business.OwnerId != userId)
            throw new NotBusinessOwnerException();
    }

    private static void ValidateHoliday(CreateHolidayRequest r)
    {
        if (r.EndDate is { } end && end < r.HolidayDate)
            throw new InvalidHolidayException("La fecha de fin no puede ser anterior a la de inicio.");

        var hasStart = r.StartTime is not null;
        var hasEnd = r.EndTime is not null;
        if (hasStart != hasEnd)
            throw new InvalidHolidayException("Para cerrar una franja horaria indica hora de inicio y de fin.");
        if (hasStart && r.StartTime >= r.EndTime)
            throw new InvalidHolidayException("La hora de inicio debe ser anterior a la de fin.");
    }

    private static void Validate(IReadOnlyList<BusinessHourInput> days)
    {
        var seen = new HashSet<int>();
        foreach (var d in days)
        {
            if (d.DayOfWeek is < 0 or > 6)
                throw new InvalidBusinessHoursException($"day_of_week inválido: {d.DayOfWeek} (debe ser 0–6).");
            if (!seen.Add(d.DayOfWeek))
                throw new InvalidBusinessHoursException($"Día duplicado: {d.DayOfWeek}.");
            if (!d.IsClosed && (d.OpeningTime is null || d.ClosingTime is null || d.OpeningTime >= d.ClosingTime))
                throw new InvalidBusinessHoursException($"Franja inválida para el día {d.DayOfWeek}: apertura debe ser anterior al cierre.");
        }
    }
}
