namespace Slotify.Domain.Entities;

/// <summary>
/// Reserva. Cliente = user XOR guest. Anti-doble-booking por staff (exclusion
/// constraint en BD) + optimistic locking (version). Schema: docs/DATA_MODEL.md.
/// </summary>
public class Reservation
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public Guid ServiceId { get; set; }
    public Service? Service { get; set; }

    /// <summary>Quién atiende (el owner es un staff). NOT NULL.</summary>
    public Guid StaffId { get; set; }
    public Staff? Staff { get; set; }

    // Cliente: exactamente uno de los dos (CHECK user_or_guest).
    public Guid? UserId { get; set; }
    public Guid? GuestId { get; set; }

    public DateTime StartTime { get; set; } // UTC
    public DateTime EndTime { get; set; }   // UTC

    public string Status { get; set; } = "pending"; // pending/confirmed/cancelled/no-show
    public string PaymentStatus { get; set; } = "not_required";

    /// <summary>Optimistic locking: se incrementa en cada UPDATE.</summary>
    public int Version { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}
