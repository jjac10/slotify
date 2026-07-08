using Microsoft.Extensions.Logging;
using Slotify.Domain.Interfaces;

namespace Slotify.Infrastructure.Notifications;

/// <summary>Destinatario de los mensajes de contacto/soporte. Sección "Support".</summary>
public class SupportOptions
{
    public string RecipientEmail { get; set; } = "soporte@slotify.app";
}

/// <summary>
/// Sender simulado de los emails de soporte (mismo seam que
/// <see cref="LoggedAccountEmailSender"/>): no envía de verdad, registra en el log el
/// mensaje de contacto marcándolo como simulado. En producción se sustituye por un
/// proveedor real de email sin tocar los servicios de dominio.
/// </summary>
public class LoggedSupportEmailSender(
    ILogger<LoggedSupportEmailSender> logger,
    SupportOptions options) : ISupportEmailSender
{
    public Task SendContactMessageAsync(string name, string email, string message, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[NOTIFY:email] (support_contact, simulado) → {Recipient}: mensaje de {SenderName} <{SenderEmail}>: {Message}",
            options.RecipientEmail, name, email, message);
        return Task.CompletedTask;
    }
}
