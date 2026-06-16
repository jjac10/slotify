namespace Slotify.Domain.Entities;

/// <summary>
/// Cliente sin registrar. Teléfono/email cifrados (AES-GCM, recuperable) +
/// blind index HMAC (búsqueda/unicidad). Schema: docs/DATA_MODEL.md (guests).
/// </summary>
public class Guest
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    public string Name { get; set; } = null!;

    // Valor recuperable (AES-256-GCM)
    public string? PhoneEncrypted { get; set; }
    public string? EmailEncrypted { get; set; }

    // Blind index (HMAC-SHA256) para lookup/UNIQUE
    public string? PhoneHash { get; set; }
    public string? EmailHash { get; set; }

    /// <summary>Enlace al user registrado (NULL mientras sea guest).</summary>
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public int TotalReservations { get; set; }
    public DateTime? LastReservationAt { get; set; }

    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
