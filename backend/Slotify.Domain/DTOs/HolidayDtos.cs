using Slotify.Domain.Entities;

namespace Slotify.Domain.DTOs;

/// <summary>Alta de un día festivo (API.md POST /businesses/{id}/holidays).</summary>
public record CreateHolidayRequest(DateOnly HolidayDate, string? Reason, bool IsClosed = true);

/// <summary>Representación de un festivo para la API.</summary>
public record BusinessHolidayResponse(Guid Id, DateOnly HolidayDate, string? Reason, bool IsClosed)
{
    public static BusinessHolidayResponse From(BusinessHoliday h) =>
        new(h.Id, h.HolidayDate, h.Reason, h.IsClosed);
}
