namespace Slotify.Domain.Entities;

/// <summary>
/// Aviso enviado (o simulado) a un cliente sobre una reserva. En el TFM el envío es
/// simulado por un sender "logged" (Status='logged'); la arquitectura permite cambiar
/// el sender por uno real (email/WhatsApp) sin tocar la lógica. Queda registrado aquí
/// como histórico/auditoría de notificaciones.
/// </summary>
public class Notification
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    /// <summary>Reserva a la que se refiere el aviso (puede haber sido borrada tras cancelar).</summary>
    public Guid ReservationId { get; set; }

    /// <summary>Canal: 'email' | 'whatsapp'.</summary>
    public string Channel { get; set; } = null!;

    /// <summary>Evento: 'created' | 'cancelled' | 'rescheduled' | 'confirmed' | 'reminder'.</summary>
    public string EventType { get; set; } = null!;

    /// <summary>Destinatario (email o teléfono) al que iría el aviso.</summary>
    public string Recipient { get; set; } = null!;

    /// <summary>Texto del aviso.</summary>
    public string Body { get; set; } = null!;

    /// <summary>Estado del envío: 'logged' (simulado), y en el futuro 'sent' | 'failed'.</summary>
    public string Status { get; set; } = "logged";

    public DateTime CreatedAt { get; set; }
}
