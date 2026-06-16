namespace Slotify.Domain.Entities;

/// <summary>
/// Día festivo/cerrado puntual de un negocio (anula el horario semanal ese día).
/// Schema: docs/DATA_MODEL.md (business_holidays).
/// </summary>
public class BusinessHoliday
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public DateOnly HolidayDate { get; set; }
    public string? Reason { get; set; }
    public bool IsClosed { get; set; } = true;
}
