namespace Slotify.Domain.Entities;

/// <summary>
/// Usuario de la plataforma. El plan NO vive aquí, vive en businesses.tier_id.
/// Schema: docs/DATA_MODEL.md (users).
/// </summary>
public class User
{
    public Guid Id { get; set; }

    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Phone { get; set; }

    /// <summary>'customer' (solo reservas) o 'owner' (tiene negocio).</summary>
    public string Type { get; set; } = "customer";

    /// <summary>'active', 'inactive', 'deleted'.</summary>
    public string Status { get; set; } = "active";

    /// <summary>
    /// Cuándo verificó su email (null = sin verificar). NO bloquea el uso de la app:
    /// solo alimenta el aviso "verifica tu email" del frontend.
    /// </summary>
    public DateTime? EmailVerifiedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<Business> Businesses { get; set; } = new List<Business>();
}
