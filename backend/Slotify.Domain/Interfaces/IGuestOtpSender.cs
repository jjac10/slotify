namespace Slotify.Domain.Interfaces;

/// <summary>
/// "Envía" el código OTP de verificación al invitado por el canal de su contacto.
/// Mismo seam swappable que el resto de envíos: email real por SMTP cuando hay
/// credenciales; el canal de teléfono queda preparado (WhatsApp vía Twilio si está
/// configurado; si no, simulado por log — cambiar a SMS real es solo otra
/// implementación de este interfaz).
/// </summary>
public interface IGuestOtpSender
{
    Task SendEmailOtpAsync(string email, string code, CancellationToken ct = default);

    /// <summary>Envía el código al teléfono E.164 (+34…).</summary>
    Task SendPhoneOtpAsync(string phoneE164, string code, CancellationToken ct = default);
}
