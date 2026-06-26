namespace Slotify.Domain.Entities;

/// <summary>
/// Servicio ofertado por un negocio. Schema: docs/DATA_MODEL.md (services).
/// Los campos de fianza/depósito (deposit_*) son futuros y se añadirán cuando se
/// implemente el cobro; este slice modela lo esencial.
/// </summary>
public class Service
{
    public Guid Id { get; set; }

    /// <summary>Negocio dueño del servicio. NOT NULL → businesses(id) (ON DELETE CASCADE).</summary>
    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>Duración en minutos (30, 60, ...).</summary>
    public int DurationMinutes { get; set; }

    /// <summary>Precio; NULL = servicio gratuito.</summary>
    public decimal? Price { get; set; }

    /// <summary>Color para el calendario (#RRGGBB).</summary>
    public string? Color { get; set; }

    /// <summary>'active', 'archived'.</summary>
    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
