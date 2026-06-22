namespace Slotify.Domain.Entities;

/// <summary>
/// Negocio. El plan/tier vive aquí (tier_id NOT NULL); los límites Freemium son
/// por negocio y un owner puede tener varios. Schema: docs/DATA_MODEL.md (businesses).
///
/// NOTA: este primer slice TDD modela solo las columnas esenciales (identidad,
/// owner, tier, nombre y estado). El resto del esquema canónico (contacto,
/// ubicación, personalización visual, config operacional, social, stats) se
/// añadirá en los slices de las features que las usen.
/// </summary>
public class Business
{
    public Guid Id { get; set; }

    /// <summary>Owner del negocio. NOT NULL → users(id).</summary>
    public Guid OwnerId { get; set; }
    public User? Owner { get; set; }

    /// <summary>Plan del negocio. NOT NULL → pricing_tiers(id) (ON DELETE RESTRICT).</summary>
    public Guid TierId { get; set; }
    public PricingTier? Tier { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>
    /// Paso de la rejilla de slots en minutos (cada cuántos minutos se ofrece un
    /// inicio). NULL → se usa la duración del servicio. Configurable por el owner.
    /// </summary>
    public int? SlotIntervalMinutes { get; set; }

    /// <summary>
    /// Zona horaria IANA del negocio (p. ej. "Europe/Madrid"). Las horas de apertura
    /// se interpretan como hora local de esta zona y se convierten a UTC para los slots.
    /// </summary>
    public string Timezone { get; set; } = "Europe/Madrid";

    /// <summary>
    /// Cómo se confirman las reservas nuevas de este negocio:
    /// <c>auto</c> → nacen 'confirmed' al instante; <c>manual</c> → nacen 'pending'
    /// y el owner/staff las confirma. Configurable por el owner. Default 'auto'.
    /// </summary>
    public string ConfirmationMode { get; set; } = "auto";

    /// <summary>
    /// Antelación mínima (en horas) con la que el cliente puede cancelar o reprogramar
    /// su reserva. Dentro de esa ventana previa al inicio ya no puede; el owner/staff
    /// sí. <c>0</c> → sin restricción. Configurable por el owner.
    /// </summary>
    public int CancellationCutoffHours { get; set; }

    /// <summary>
    /// Cómo opera el negocio en cuanto a reservas:
    /// <c>online</c> → los clientes reservan por internet (aparece en Explorar);
    /// <c>calendar_only</c> → solo el owner/staff apunta reservas desde la Agenda
    /// (no se ofrece reserva pública ni sale en Explorar). Configurable por el owner.
    /// </summary>
    public string BookingMode { get; set; } = "online";

    // --- Notificaciones (avisos a clientes) ---

    /// <summary>Enviar avisos al cliente por email (reserva creada/cancelada/reprogramada + recordatorio).</summary>
    public bool NotifyByEmail { get; set; } = true;

    /// <summary>Enviar avisos al cliente por WhatsApp. Por defecto desactivado.</summary>
    public bool NotifyByWhatsapp { get; set; }

    /// <summary>
    /// Antelación (en horas) del recordatorio de la cita. <c>0</c> → sin recordatorio.
    /// Configurable por el owner.
    /// </summary>
    public int ReminderHoursBefore { get; set; } = 24;

    // --- Perfil público (para Explorar) ---

    /// <summary>Categoría del negocio (código: 'peluqueria', 'barberia', …). NULL = sin categorizar.</summary>
    public string? Category { get; set; }

    /// <summary>URL de una foto del negocio (para las tarjetas de Explorar). NULL = sin foto.</summary>
    public string? PhotoUrl { get; set; }

    /// <summary>Ubicación (para "negocios cercanos"). NULL si el owner no la ha fijado.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>Media de valoraciones (1–5). NULL si aún no tiene reseñas. Denormalizado.</summary>
    public double? Rating { get; set; }

    /// <summary>Número de reseñas. Denormalizado.</summary>
    public int ReviewCount { get; set; }

    /// <summary>'active', 'inactive', 'deleted'.</summary>
    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
