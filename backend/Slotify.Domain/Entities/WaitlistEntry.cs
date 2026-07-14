namespace Slotify.Domain.Entities;

/// <summary>
/// Entrada en la lista de espera de un (servicio, día): si el día está completo, el
/// cliente se apunta y, cuando una cancelación libera hueco, se avisa al primero de
/// la cola (status waiting → notified). En esta versión solo usuarios registrados;
/// <see cref="GuestId"/> queda preparado para invitados (mismo CHECK user_or_guest
/// que reservations).
/// </summary>
public class WaitlistEntry
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    public Guid ServiceId { get; set; }

    /// <summary>Día (local del negocio) para el que se espera hueco.</summary>
    public DateOnly Date { get; set; }

    public Guid? UserId { get; set; }

    /// <summary>Preparado para invitados (no se usa aún).</summary>
    public Guid? GuestId { get; set; }

    /// <summary>Posición en la cola de ese (servicio, día): 1, 2, 3…</summary>
    public int Position { get; set; }

    /// <summary>'waiting' | 'notified'.</summary>
    public string Status { get; set; } = "waiting";

    public DateTime? NotifiedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public Business? Business { get; set; }

    public Service? Service { get; set; }
}
