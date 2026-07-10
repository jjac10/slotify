namespace Slotify.Domain.Entities;

/// <summary>
/// Código de verificación (OTP) de un invitado: 6 dígitos que viajan por email o
/// teléfono y de los que solo se persiste el hash SHA-256. El contacto se guarda
/// como blind index (HMAC), igual que en <see cref="Guest"/>: nunca en claro.
/// Caduca a los 10 minutos y admite un máximo de intentos fallidos.
/// </summary>
public class GuestOtpCode
{
    public Guid Id { get; set; }

    /// <summary>Blind index (HMAC) del contacto normalizado (teléfono o email).</summary>
    public string ContactHash { get; set; } = null!;

    /// <summary>SHA-256 hex del código de 6 dígitos (nunca el código en claro).</summary>
    public string CodeHash { get; set; } = null!;

    /// <summary>Intentos fallidos de verificación acumulados.</summary>
    public int Attempts { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
