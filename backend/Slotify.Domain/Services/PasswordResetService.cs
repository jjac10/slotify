using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Recuperación de contraseña: emite tokens de un solo uso (256 bits aleatorios, solo
/// se persiste el hash SHA-256, caducidad 1 h) que viajan en un email simulado, y
/// restablece la contraseña invalidando las sesiones (refresh tokens) del usuario.
/// </summary>
public class PasswordResetService(
    IAuthRepository auth,
    IPasswordResetTokenRepository resetTokens,
    IPasswordHasher hasher,
    IRefreshTokenRepository refreshTokens,
    IPasswordResetEmailSender mailer)
{
    /// <summary>Vida útil del token de recuperación.</summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    private const int TokenBytes = 32; // 256 bits aleatorios criptográficos

    /// <summary>
    /// Genera un token para el email dado y lo "envía" (simulado). Si el email no
    /// existe no hace nada: el endpoint responde igual en ambos casos para no
    /// permitir enumerar usuarios.
    /// </summary>
    public async Task RequestResetAsync(string email, CancellationToken ct = default)
    {
        var user = await auth.GetByEmailAsync(email, ct);
        if (user is null)
            return; // silencioso: anti-enumeración

        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        await resetTokens.AddAsync(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Hash(token),
            ExpiresAt = DateTime.UtcNow.Add(TokenLifetime),
        }, ct);

        await mailer.SendAsync(user.Email, token, ct);
    }

    /// <summary>
    /// Valida el token (hash, no caducado, no usado), cambia la contraseña, marca el
    /// token como usado y revoca los refresh tokens activos del usuario.
    /// </summary>
    public async Task ResetPasswordAsync(string token, string newPassword, CancellationToken ct = default)
    {
        PasswordPolicy.Validate(newPassword);

        var entity = await resetTokens.GetByHashAsync(Hash(token), ct);
        if (entity is null || entity.UsedAt is not null || entity.ExpiresAt <= DateTime.UtcNow)
            throw new InvalidPasswordResetTokenException();

        var user = await auth.GetByIdAsync(entity.UserId, ct)
            ?? throw new InvalidPasswordResetTokenException();

        user.PasswordHash = hasher.Hash(newPassword);
        await auth.UpdateUserAsync(user, ct);

        entity.UsedAt = DateTime.UtcNow;
        await resetTokens.UpdateAsync(entity, ct);

        // Cambiar la contraseña cierra las sesiones abiertas (mismo criterio que ADR #3).
        await refreshTokens.RevokeAllForUserAsync(user.Id, ct);
    }

    private static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
