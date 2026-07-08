using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;

namespace Slotify.Infrastructure.Notifications;

/// <summary>
/// Configuración SMTP desde variables de entorno (<c>SMTP_HOST</c>, <c>SMTP_PORT</c>,
/// <c>SMTP_USER</c>, <c>SMTP_PASSWORD</c> y opcional <c>SMTP_FROM</c>). Si faltan las
/// credenciales (<see cref="IsConfigured"/> = false, p. ej. en desarrollo local), la app
/// mantiene el envío simulado por log.
/// </summary>
public class SmtpOptions
{
    public string? Host { get; set; }

    /// <summary>587 = submission con STARTTLS (IONOS).</summary>
    public int Port { get; set; } = 587;

    public string? User { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = "slotify@jjalarcon.es";

    public string FromName { get; set; } = "Slotify";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(User) &&
        !string.IsNullOrWhiteSpace(Password);

    public static SmtpOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new SmtpOptions
        {
            Host = configuration["SMTP_HOST"],
            User = configuration["SMTP_USER"],
            Password = configuration["SMTP_PASSWORD"],
        };
        if (int.TryParse(configuration["SMTP_PORT"], out var port))
        {
            options.Port = port;
        }
        if (!string.IsNullOrWhiteSpace(configuration["SMTP_FROM"]))
        {
            options.FromAddress = configuration["SMTP_FROM"]!;
        }
        return options;
    }
}

/// <summary>
/// Transporte SMTP inyectable: separa la construcción del mensaje (testeable en unit
/// tests) del envío real por la red (MailKit).
/// </summary>
public interface ISmtpTransport
{
    Task SendAsync(MimeMessage message, CancellationToken ct = default);
}

/// <summary>Envío real con MailKit: conexión STARTTLS + login por mensaje.</summary>
public class MailKitSmtpTransport(SmtpOptions options) : ISmtpTransport
{
    public async Task SendAsync(MimeMessage message, CancellationToken ct = default)
    {
        if (!options.IsConfigured)
        {
            throw new InvalidOperationException("Transporte SMTP sin configurar (faltan SMTP_HOST/SMTP_USER/SMTP_PASSWORD).");
        }

        using var client = new SmtpClient();
        await client.ConnectAsync(options.Host!, options.Port, SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(options.User!, options.Password!, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }
}

/// <summary>
/// Sender real de emails por SMTP para los tres seams de correo del proyecto
/// (cuenta, avisos de reservas y contacto/soporte). Los avisos de canal 'whatsapp'
/// no tienen proveedor real: caen al sender simulado por log.
/// </summary>
public class SmtpEmailSender(
    ISmtpTransport transport,
    SmtpOptions options,
    FrontendOptions frontend,
    SupportOptions support,
    LoggedNotificationSender simulatedFallback,
    ILogger<SmtpEmailSender> logger) : IAccountEmailSender, INotificationSender, ISupportEmailSender
{
    // --- IAccountEmailSender ---

    public Task SendPasswordResetAsync(string email, string token, CancellationToken ct = default)
        => SendAsync(
            to: email,
            subject: "Restablece tu contraseña de Slotify",
            body: $"""
                Hola:

                Hemos recibido una solicitud para restablecer tu contraseña de Slotify.
                Puedes hacerlo desde este enlace (caduca en 1 hora):

                {Link("restablecer", token)}

                Si no has sido tú, ignora este mensaje: tu contraseña no cambiará.

                — Slotify
                """,
            kind: "password_reset",
            ct: ct);

    public Task SendEmailVerificationAsync(string email, string token, CancellationToken ct = default)
        => SendAsync(
            to: email,
            subject: "Verifica tu email de Slotify",
            body: $"""
                Hola:

                Confirma tu dirección de email desde este enlace (caduca en 24 horas):

                {Link("verificar-email", token)}

                Si no has creado una cuenta en Slotify, ignora este mensaje.

                — Slotify
                """,
            kind: "email_verification",
            ct: ct);

    // --- INotificationSender ---

    public Task SendAsync(Notification notification, CancellationToken ct = default)
    {
        // Solo hay proveedor real para email; WhatsApp sigue simulado por log.
        if (notification.Channel != "email")
        {
            return simulatedFallback.SendAsync(notification, ct);
        }

        return SendAsync(
            to: notification.Recipient,
            subject: SubjectFor(notification.EventType),
            body: $"{notification.Body}\n\n— Slotify",
            kind: notification.EventType,
            ct: ct);
    }

    private static string SubjectFor(string eventType) => eventType switch
    {
        "created" => "Slotify — Reserva creada",
        "confirmed" => "Slotify — Reserva confirmada",
        "cancelled" => "Slotify — Reserva cancelada",
        "rescheduled" => "Slotify — Reserva reprogramada",
        "reminder" => "Slotify — Recordatorio de tu reserva",
        _ => "Slotify — Aviso sobre tu reserva",
    };

    // --- ISupportEmailSender ---

    public Task SendContactMessageAsync(string name, string email, string message, CancellationToken ct = default)
        => SendAsync(
            to: support.RecipientEmail,
            subject: $"Contacto Slotify: {name}",
            body: $"""
                Mensaje del formulario de contacto de Slotify.

                Nombre: {name}
                Email: {email}

                {message}
                """,
            kind: "support_contact",
            replyTo: new MailboxAddress(name, email),
            ct: ct);

    // --- Común ---

    private string Link(string path, string token)
        => $"{frontend.BaseUrl.TrimEnd('/')}/{path}?token={Uri.EscapeDataString(token)}";

    private async Task SendAsync(
        string to, string subject, string body, string kind,
        MailboxAddress? replyTo = null, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        if (replyTo is not null)
        {
            message.ReplyTo.Add(replyTo);
        }
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        await transport.SendAsync(message, ct);
        // Sin datos personales más allá del destinatario, como el resto del logging.
        logger.LogInformation("[SMTP:email] ({Kind}) → {Recipient}: enviado", kind, to);
    }
}
