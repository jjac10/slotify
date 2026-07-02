namespace Slotify.Domain.Entities;

/// <summary>
/// Token de recuperación de contraseña. Solo se persiste su hash SHA-256 (el valor
/// en claro viaja únicamente en el "email" simulado); caduca a la hora y es de un
/// solo uso (<see cref="UsedAt"/>).
/// </summary>
public class PasswordResetToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Hash SHA-256 (hex) del token; nunca se guarda el valor en claro. UNIQUE.</summary>
    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    /// <summary>Cuándo se consumió (null = aún utilizable). Un solo uso.</summary>
    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
