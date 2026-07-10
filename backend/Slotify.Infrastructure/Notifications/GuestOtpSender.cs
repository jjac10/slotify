using Microsoft.Extensions.Logging;
using MimeKit;
using Slotify.Domain.Interfaces;

namespace Slotify.Infrastructure.Notifications;

/// <summary>
/// Envío del código OTP de invitado por el canal del contacto, con el mismo criterio
/// que el resto de la fontanería: cada canal es real si su transporte está configurado
/// y simulado (log) si no. Email → SMTP; teléfono → WhatsApp vía Twilio (el seam para
/// SMS real es este mismo interfaz: otra implementación y listo).
/// </summary>
public class GuestOtpSender(
    ISmtpTransport? smtp,
    IWhatsAppTransport? whatsApp,
    SmtpOptions smtpOptions,
    ILogger<GuestOtpSender> logger) : IGuestOtpSender
{
    public async Task SendEmailOtpAsync(string email, string code, CancellationToken ct = default)
    {
        if (smtp is null)
        {
            // Simulado (desarrollo): el código solo queda en el log, como el resto de emails.
            logger.LogInformation("[NOTIFY:email] (guest_otp) → {Recipient}: Tu código de verificación de Slotify es {Code} (caduca en 10 minutos)", email, code);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtpOptions.FromName, smtpOptions.FromAddress));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Tu código de verificación de Slotify";
        message.Body = new TextPart("plain")
        {
            Text = $"""
                Hola:

                Tu código para ver y gestionar tus reservas es:

                {code}

                Caduca en 10 minutos. Si no lo has pedido tú, ignora este mensaje.

                — Slotify
                """,
        };
        await smtp.SendAsync(message, ct);
        logger.LogInformation("[SMTP:email] (guest_otp) → {Recipient}: enviado", email);
    }

    public async Task SendPhoneOtpAsync(string phoneE164, string code, CancellationToken ct = default)
    {
        if (whatsApp is null)
        {
            logger.LogInformation("[NOTIFY:phone] (guest_otp) → {Recipient}: Tu código de verificación de Slotify es {Code} (caduca en 10 minutos)", phoneE164, code);
            return;
        }

        await whatsApp.SendAsync(phoneE164, $"Tu código de verificación de Slotify es {code}. Caduca en 10 minutos.", ct);
        logger.LogInformation("[TWILIO:whatsapp] (guest_otp) → {Recipient}: enviado", phoneE164);
    }
}
