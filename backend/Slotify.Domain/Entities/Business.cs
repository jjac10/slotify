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

    /// <summary>'active', 'inactive', 'deleted'.</summary>
    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
