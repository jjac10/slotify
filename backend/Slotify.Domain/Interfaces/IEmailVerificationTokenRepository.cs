using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>Persistencia de tokens de verificación de email (solo hashes).</summary>
public interface IEmailVerificationTokenRepository
{
    Task AddAsync(EmailVerificationToken token, CancellationToken ct = default);

    /// <summary>Busca un token por su hash SHA-256 (hex). Null si no existe.</summary>
    Task<EmailVerificationToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Persiste cambios en un token ya cargado (p. ej. marcarlo usado).</summary>
    Task UpdateAsync(EmailVerificationToken token, CancellationToken ct = default);
}
