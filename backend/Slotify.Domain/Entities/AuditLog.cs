namespace Slotify.Domain.Entities;

/// <summary>
/// Registro de auditoría de acciones sobre reservas (ADR #14). Sobrevive al
/// hard-delete de la reserva (reservation_id ON DELETE SET NULL); el detalle
/// queda en old_values/new_values (JSONB). Schema: docs/DATA_MODEL.md (audit_logs).
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }

    /// <summary>Reserva afectada. Se anula si la reserva se borra (la auditoría persiste).</summary>
    public Guid? ReservationId { get; set; }

    /// <summary>created, updated, cancelled, no-show...</summary>
    public string Action { get; set; } = null!;

    public Guid? ActorId { get; set; }
    public Guid? GuestId { get; set; }

    /// <summary>owner, registered_user, guest, system.</summary>
    public string? ActorType { get; set; }

    public string? OldValues { get; set; } // JSONB
    public string? NewValues { get; set; } // JSONB

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }
}
