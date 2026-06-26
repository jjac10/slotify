namespace Slotify.Domain.Entities;

/// <summary>
/// Horario semanal de un negocio (una fila por día). Schema: docs/DATA_MODEL.md
/// (business_hours). day_of_week: 0=domingo ... 6=sábado.
/// </summary>
public class BusinessHour
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public int DayOfWeek { get; set; }

    public bool IsClosed { get; set; }

    /// <summary>NULL si el día está cerrado.</summary>
    public TimeOnly? OpeningTime { get; set; }
    public TimeOnly? ClosingTime { get; set; }
}
