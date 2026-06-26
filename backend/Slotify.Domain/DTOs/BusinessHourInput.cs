namespace Slotify.Domain.DTOs;

/// <summary>Una entrada del horario semanal (día). day_of_week: 0=domingo ... 6=sábado.</summary>
public record BusinessHourInput(int DayOfWeek, bool IsClosed, TimeOnly? OpeningTime, TimeOnly? ClosingTime);

/// <summary>Petición para fijar el horario semanal completo (API.md PUT /businesses/{id}/hours).</summary>
public record SetBusinessHoursRequest(IReadOnlyList<BusinessHourInput> Days);

/// <summary>Representación de un día del horario para la API.</summary>
public record BusinessHourResponse(int DayOfWeek, bool IsClosed, TimeOnly? OpeningTime, TimeOnly? ClosingTime)
{
    public static BusinessHourResponse From(Slotify.Domain.Entities.BusinessHour h) =>
        new(h.DayOfWeek, h.IsClosed, h.OpeningTime, h.ClosingTime);
}
