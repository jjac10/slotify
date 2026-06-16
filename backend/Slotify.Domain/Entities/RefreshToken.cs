namespace Slotify.Domain.Entities;

/// <summary>
/// Refresh token persistido (hasheado) para renovar el access token (ADR #3).
/// Schema: docs/DATA_MODEL.md (refresh_tokens).
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Hash del token (no se guarda el valor en claro). UNIQUE.</summary>
    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
