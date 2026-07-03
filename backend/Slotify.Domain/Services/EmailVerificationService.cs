using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Verificación de email NO bloqueante: al registrarse se "envía" (simulado) un enlace
/// con un token de un solo uso (256 bits, solo hash SHA-256 en BD, 24 h de vida) que
/// marca <c>users.email_verified_at</c>. El usuario puede usar la app sin verificar;
/// el frontend solo muestra un aviso. Mismo criterio que <see cref="PasswordResetService"/>.
/// </summary>
public class EmailVerificationService(
    IAuthRepository auth,
    IEmailVerificationTokenRepository verificationTokens,
    IAccountEmailSender mailer)
{
    /// <summary>Vida útil del token de verificación.</summary>
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    /// <summary>
    /// Envío best-effort tras el registro: genera el token y "envía" el email, pero
    /// NUNCA propaga errores — el registro no debe fallar porque falle el email.
    /// No-op si el usuario no existe o ya está verificado.
    /// </summary>
    public async Task TrySendAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var user = await auth.GetByIdAsync(userId, ct);
            if (user is null || user.EmailVerifiedAt is not null)
                return;

            await IssueAndSendAsync(user, ct);
        }
        catch
        {
            // Best-effort: el aviso del frontend permite reenviar el enlace después.
        }
    }

    /// <summary>
    /// Reenvío explícito (usuario autenticado): regenera el token y lo "envía".
    /// Lanza <see cref="EmailAlreadyVerifiedException"/> si ya está verificado.
    /// </summary>
    public async Task ResendAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await auth.GetByIdAsync(userId, ct);
        if (user is null)
            return; // sesión válida sin usuario: no hay nada que reenviar

        if (user.EmailVerifiedAt is not null)
            throw new EmailAlreadyVerifiedException();

        await IssueAndSendAsync(user, ct);
    }

    /// <summary>
    /// Valida el token (hash, no caducado, no usado), marca el email como verificado
    /// (idempotente: no re-escribe la fecha si ya lo estaba) y consume el token.
    /// </summary>
    public async Task VerifyAsync(string token, CancellationToken ct = default)
    {
        var entity = await verificationTokens.GetByHashAsync(AccountTokens.Sha256Hex(token), ct);
        if (entity is null || entity.UsedAt is not null || entity.ExpiresAt <= DateTime.UtcNow)
            throw new InvalidEmailVerificationTokenException();

        var user = await auth.GetByIdAsync(entity.UserId, ct)
            ?? throw new InvalidEmailVerificationTokenException();

        if (user.EmailVerifiedAt is null)
        {
            user.EmailVerifiedAt = DateTime.UtcNow;
            await auth.UpdateUserAsync(user, ct);
        }

        entity.UsedAt = DateTime.UtcNow;
        await verificationTokens.UpdateAsync(entity, ct);
    }

    /// <summary>Estado de verificación (para /auth/me).</summary>
    public async Task<bool> IsVerifiedAsync(Guid userId, CancellationToken ct = default)
        => (await auth.GetByIdAsync(userId, ct))?.EmailVerifiedAt is not null;

    private async Task IssueAndSendAsync(User user, CancellationToken ct)
    {
        var token = AccountTokens.NewToken();
        await verificationTokens.AddAsync(new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = AccountTokens.Sha256Hex(token),
            ExpiresAt = DateTime.UtcNow.Add(TokenLifetime),
        }, ct);

        await mailer.SendEmailVerificationAsync(user.Email, token, ct);
    }
}
