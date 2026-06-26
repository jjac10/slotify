namespace Slotify.Domain.Entities;

/// <summary>
/// Día(s) festivo(s)/cerrado(s) de un negocio (anula el horario semanal). Puede ser
/// un día suelto, un rango de días (<see cref="EndDate"/>) y/o solo una franja horaria
/// (<see cref="StartTime"/>/<see cref="EndTime"/>). Schema: docs/DATA_MODEL.md.
/// </summary>
public class BusinessHoliday
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    /// <summary>Primer día (incl.).</summary>
    public DateOnly HolidayDate { get; set; }

    /// <summary>Último día del rango (incl.). NULL = un solo día (<see cref="HolidayDate"/>).</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>Inicio de la franja cerrada (hora local). NULL = día(s) completo(s).</summary>
    public TimeOnly? StartTime { get; set; }

    /// <summary>Fin de la franja cerrada (hora local). NULL = día(s) completo(s).</summary>
    public TimeOnly? EndTime { get; set; }

    public string? Reason { get; set; }
    public bool IsClosed { get; set; } = true;
}
