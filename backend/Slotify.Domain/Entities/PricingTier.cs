namespace Slotify.Domain.Entities;

/// <summary>
/// Plan de precios (data-driven). Añadir un plan = INSERT, sin tocar código (ADR #9).
/// Los límites NULL significan "ilimitado". Schema: docs/DATA_MODEL.md (pricing_tiers).
/// </summary>
public class PricingTier
{
    public Guid Id { get; set; }

    /// <summary>Clave estable usada en código ('free', 'premium'). UNIQUE.</summary>
    public string Code { get; set; } = null!;

    /// <summary>Nombre visible.</summary>
    public string Name { get; set; } = null!;

    // Límites (NULL = ilimitado)
    public int? MaxReservationsPerMonth { get; set; }
    public int? MaxClients { get; set; }
    public int? MaxServices { get; set; }
    public int? MaxStaff { get; set; }

    // Feature flags
    public bool ChannelEmail { get; set; } = true;
    public bool ChannelSms { get; set; }
    public bool ChannelWhatsapp { get; set; }
    public bool HasAnalytics { get; set; }
    public bool HasApi { get; set; }

    /// <summary>Precio informativo (el cobro real es futuro).</summary>
    public decimal PriceMonthly { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ICollection<Business> Businesses { get; set; } = new List<Business>();
}
