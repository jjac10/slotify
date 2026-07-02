using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>Persistencia de tokens de recuperación de contraseña (solo hashes).</summary>
public interface IPasswordResetTokenRepository
{
    Task AddAsync(PasswordResetToken token, CancellationToken ct = default);

    /// <summary>Busca un token por su hash SHA-256 (hex). Null si no existe.</summary>
    Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Persiste cambios en un token ya cargado (p. ej. marcarlo usado).</summary>
    Task UpdateAsync(PasswordResetToken token, CancellationToken ct = default);
}
