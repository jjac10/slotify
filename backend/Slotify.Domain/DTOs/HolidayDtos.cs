using Slotify.Domain.Entities;

namespace Slotify.Domain.DTOs;

/// <summary>
/// Alta de un festivo/cierre (POST /businesses/{id}/holidays). <c>EndDate</c> opcional
/// para un rango de días; <c>StartTime</c>/<c>EndTime</c> opcionales para cerrar solo
/// una franja horaria (ambos o ninguno).
/// </summary>
public record CreateHolidayRequest(
    DateOnly HolidayDate,
    string? Reason,
    bool IsClosed = true,
    DateOnly? EndDate = null,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null);

/// <summary>Representación de un festivo para la API.</summary>
public record BusinessHolidayResponse(
    Guid Id, DateOnly HolidayDate, string? Reason, bool IsClosed,
    DateOnly? EndDate, TimeOnly? StartTime, TimeOnly? EndTime)
{
    public static BusinessHolidayResponse From(BusinessHoliday h) =>
        new(h.Id, h.HolidayDate, h.Reason, h.IsClosed, h.EndDate, h.StartTime, h.EndTime);
}
