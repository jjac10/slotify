using System.Security.Cryptography;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Verificación de identidad del invitado antes de ver/gestionar sus reservas por
/// contacto (cierra el TODO de seguridad del lookup): emite códigos de 6 dígitos
/// (solo hash en BD, caducidad corta, máximo de intentos) y los verifica. El código
/// es reutilizable dentro de su ventana: ver reservas y luego cancelar/reprogramar
/// con el mismo. Pedir un código nunca revela si el contacto tiene reservas
/// (anti-enumeración: se envía igualmente).
/// </summary>
public class GuestOtpService(
    IGuestOtpRepository codes,
    IGuestOtpSender sender,
    IBlindIndex blindIndex)
{
    /// <summary>Vida útil del código.</summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    /// <summary>Intentos fallidos permitidos antes de invalidar el código.</summary>
    public const int MaxAttempts = 5;

    /// <summary>
    /// Genera un código para el contacto y lo envía por su canal (email o teléfono).
    /// Silencioso con contacto en blanco; el endpoint responde igual en todos los
    /// casos (anti-enumeración) y va rate-limited contra el abuso.
    /// </summary>
    public async Task RequestCodeAsync(string? contact, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(contact))
            return;

        var (normalized, isEmail) = Normalize(contact);
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        await codes.AddAsync(new GuestOtpCode
        {
            Id = Guid.NewGuid(),
            ContactHash = blindIndex.Compute(normalized),
            CodeHash = AccountTokens.Sha256Hex(code),
            Attempts = 0,
            ExpiresAt = DateTime.UtcNow.Add(CodeLifetime),
            CreatedAt = DateTime.UtcNow,
        }, ct);

        if (isEmail)
            await sender.SendEmailOtpAsync(normalized, code, ct);
        else
            await sender.SendPhoneOtpAsync(normalized, code, ct);
    }

    /// <summary>
    /// ¿El código es el más reciente del contacto, no ha caducado y no ha agotado los
    /// intentos? Un fallo suma intento; un acierto no lo consume (reutilizable en la
    /// ventana para lookup + cancelar/reprogramar).
    /// </summary>
    public async Task<bool> VerifyAsync(string? contact, string? code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(contact) || string.IsNullOrWhiteSpace(code))
            return false;

        var (normalized, _) = Normalize(contact);
        var entity = await codes.GetLatestByContactHashAsync(blindIndex.Compute(normalized), ct);
        if (entity is null || entity.ExpiresAt <= DateTime.UtcNow || entity.Attempts >= MaxAttempts)
            return false;

        if (AccountTokens.Sha256Hex(code.Trim()) == entity.CodeHash)
            return true;

        entity.Attempts++;
        await codes.UpdateAsync(entity, ct);
        return false;
    }

    /// <summary>Misma normalización que el alta de invitados y el lookup (blind index compatible).</summary>
    private static (string Normalized, bool IsEmail) Normalize(string contact) =>
        contact.Contains('@')
            ? (ContactNormalizer.NormalizeEmail(contact), true)
            : (ContactNormalizer.NormalizePhone(contact), false);
}
