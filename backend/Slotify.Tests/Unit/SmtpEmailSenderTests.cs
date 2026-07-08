using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using Moq;
using Slotify.Domain.Entities;
using Slotify.Infrastructure.Notifications;

namespace Slotify.Tests.Unit;

/// <summary>
/// Envío real de emails por SMTP: <see cref="SmtpEmailSender"/> construye el MimeMessage
/// correcto (remitente configurado, destinatario, asunto y cuerpo en español con el
/// enlace) y delega el envío en <see cref="ISmtpTransport"/>. Los avisos de canal
/// 'whatsapp' NO van por SMTP: caen al sender simulado. <see cref="SmtpOptions"/> decide
/// si hay configuración suficiente (fallback a envío simulado si no).
/// </summary>
public class SmtpEmailSenderTests
{
    private readonly Mock<ISmtpTransport> _transport = new();
    private readonly List<MimeMessage> _sent = [];

    private SmtpEmailSender CreateSender()
    {
        _transport
            .Setup(t => t.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>()))
            .Callback<MimeMessage, CancellationToken>((m, _) => _sent.Add(m))
            .Returns(Task.CompletedTask);

        return new SmtpEmailSender(
            _transport.Object,
            new SmtpOptions { Host = "smtp.ionos.es", User = "slotify@jjalarcon.es", Password = "secret" },
            new FrontendOptions { BaseUrl = "https://slotify.jjalarcon.es" },
            new SupportOptions { RecipientEmail = "soporte@slotify.app" },
            new LoggedNotificationSender(NullLogger<LoggedNotificationSender>.Instance),
            NullLogger<SmtpEmailSender>.Instance);
    }

    private static Notification EmailNotification(string eventType = "created") => new()
    {
        Id = Guid.NewGuid(),
        BusinessId = Guid.NewGuid(),
        ReservationId = Guid.NewGuid(),
        Channel = "email",
        EventType = eventType,
        Recipient = "cliente@example.com",
        Body = "Tu reserva en Barbería Elite es el 10/07 a las 16:30.",
    };

    // --- Emails de cuenta (IAccountEmailSender) ---

    [Fact]
    public async Task SendPasswordResetAsync_BuildsSpanishEmailWithResetLink()
    {
        await CreateSender().SendPasswordResetAsync("ana@example.com", "tok/en+raro");

        var message = Assert.Single(_sent);
        Assert.Equal("ana@example.com", ((MailboxAddress)message.To[0]).Address);
        Assert.Equal("slotify@jjalarcon.es", ((MailboxAddress)message.From[0]).Address);
        Assert.Contains("contraseña", message.Subject);
        Assert.Contains("https://slotify.jjalarcon.es/restablecer?token=tok%2Fen%2Braro", message.TextBody);
    }

    [Fact]
    public async Task SendEmailVerificationAsync_BuildsSpanishEmailWithVerificationLink()
    {
        await CreateSender().SendEmailVerificationAsync("ana@example.com", "token123");

        var message = Assert.Single(_sent);
        Assert.Contains("Verifica", message.Subject);
        Assert.Contains("https://slotify.jjalarcon.es/verificar-email?token=token123", message.TextBody);
    }

    // --- Avisos de reservas (INotificationSender) ---

    [Fact]
    public async Task SendAsync_EmailChannel_SendsBodyViaSmtpWithEventSubject()
    {
        await CreateSender().SendAsync(EmailNotification("cancelled"));

        var message = Assert.Single(_sent);
        Assert.Equal("cliente@example.com", ((MailboxAddress)message.To[0]).Address);
        Assert.Contains("cancelada", message.Subject);
        Assert.Contains("Tu reserva en Barbería Elite", message.TextBody);
    }

    [Theory]
    [InlineData("created", "creada")]
    [InlineData("confirmed", "confirmada")]
    [InlineData("rescheduled", "reprogramada")]
    [InlineData("reminder", "Recordatorio")]
    public async Task SendAsync_EmailChannel_SubjectMatchesEventType(string eventType, string expectedInSubject)
    {
        await CreateSender().SendAsync(EmailNotification(eventType));

        Assert.Contains(expectedInSubject, Assert.Single(_sent).Subject);
    }

    [Fact]
    public async Task SendAsync_WhatsappChannel_DoesNotUseSmtp()
    {
        var notification = EmailNotification();
        notification.Channel = "whatsapp";
        notification.Recipient = "+34600111222";

        await CreateSender().SendAsync(notification);

        // Sin proveedor de WhatsApp: cae al sender simulado, nunca al transporte SMTP.
        _transport.Verify(t => t.SendAsync(It.IsAny<MimeMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Contacto/soporte (ISupportEmailSender) ---

    [Fact]
    public async Task SendContactMessageAsync_SendsToPlatformOwnerWithReplyTo()
    {
        await CreateSender().SendContactMessageAsync("Ana García", "ana@example.com", "Hola, una duda.");

        var message = Assert.Single(_sent);
        Assert.Equal("soporte@slotify.app", ((MailboxAddress)message.To[0]).Address);
        Assert.Equal("ana@example.com", ((MailboxAddress)message.ReplyTo[0]).Address);
        Assert.Contains("Ana García", message.Subject);
        Assert.Contains("Hola, una duda.", message.TextBody);
        Assert.Contains("ana@example.com", message.TextBody);
    }
}

/// <summary>SMTP_HOST/PORT/USER/PASSWORD desde el entorno; sin credenciales → no configurado.</summary>
public class SmtpOptionsTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
            .Build();

    [Fact]
    public void FromConfiguration_AllVariables_IsConfiguredWithGivenValues()
    {
        var options = SmtpOptions.FromConfiguration(Config(
            ("SMTP_HOST", "smtp.ionos.es"),
            ("SMTP_PORT", "465"),
            ("SMTP_USER", "slotify@jjalarcon.es"),
            ("SMTP_PASSWORD", "secret")));

        Assert.True(options.IsConfigured);
        Assert.Equal("smtp.ionos.es", options.Host);
        Assert.Equal(465, options.Port);
        Assert.Equal("slotify@jjalarcon.es", options.User);
        Assert.Equal("secret", options.Password);
    }

    [Fact]
    public void FromConfiguration_NoVariables_IsNotConfigured_PortDefaults587()
    {
        var options = SmtpOptions.FromConfiguration(Config());

        Assert.False(options.IsConfigured);
        Assert.Equal(587, options.Port);
        // Remitente por defecto del proyecto (IONOS).
        Assert.Equal("slotify@jjalarcon.es", options.FromAddress);
    }

    [Theory]
    [InlineData("SMTP_HOST")]
    [InlineData("SMTP_USER")]
    [InlineData("SMTP_PASSWORD")]
    public void FromConfiguration_MissingAnyCredential_IsNotConfigured(string missingKey)
    {
        var all = new List<(string, string)>
        {
            ("SMTP_HOST", "smtp.ionos.es"), ("SMTP_USER", "user"), ("SMTP_PASSWORD", "pass"),
        };
        all.RemoveAll(v => v.Item1 == missingKey);

        Assert.False(SmtpOptions.FromConfiguration(Config(all.ToArray())).IsConfigured);
    }
}
