using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;

namespace Slotify.Infrastructure.Notifications;

/// <summary>
/// Configuración de Twilio para WhatsApp desde variables de entorno
/// (<c>TWILIO_ACCOUNT_SID</c>, <c>TWILIO_AUTH_TOKEN</c>, <c>TWILIO_WHATSAPP_FROM</c>).
/// En desarrollo se usa el sandbox de Twilio (From <c>+14155238886</c>, los destinatarios
/// se unen con el código del sandbox); si faltan variables (<see cref="IsConfigured"/> =
/// false), los avisos de WhatsApp siguen simulados por log.
/// </summary>
public class TwilioOptions
{
    public string? AccountSid { get; set; }

    public string? AuthToken { get; set; }

    /// <summary>Número emisor en E.164 (sandbox: +14155238886).</summary>
    public string? WhatsAppFrom { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountSid) &&
        !string.IsNullOrWhiteSpace(AuthToken) &&
        !string.IsNullOrWhiteSpace(WhatsAppFrom);

    public static TwilioOptions FromConfiguration(IConfiguration configuration) => new()
    {
        AccountSid = configuration["TWILIO_ACCOUNT_SID"],
        AuthToken = configuration["TWILIO_AUTH_TOKEN"],
        WhatsAppFrom = configuration["TWILIO_WHATSAPP_FROM"],
    };
}

/// <summary>
/// Transporte de WhatsApp inyectable: separa el enrutado del aviso (testeable) del
/// envío real por HTTP contra la API de Twilio.
/// </summary>
public interface IWhatsAppTransport
{
    /// <summary>Envía un mensaje de WhatsApp a un teléfono E.164 (+34…).</summary>
    Task SendAsync(string toE164, string body, CancellationToken ct = default);
}

/// <summary>
/// Envío real vía Twilio: POST form-urlencoded a
/// <c>/2010-04-01/Accounts/{sid}/Messages.json</c> con Basic auth y el prefijo
/// <c>whatsapp:</c> en origen y destino.
/// </summary>
public class TwilioWhatsAppTransport(HttpClient httpClient, TwilioOptions options) : IWhatsAppTransport
{
    public async Task SendAsync(string toE164, string body, CancellationToken ct = default)
    {
        if (!options.IsConfigured)
        {
            throw new InvalidOperationException(
                "Transporte de WhatsApp sin configurar (faltan TWILIO_ACCOUNT_SID/TWILIO_AUTH_TOKEN/TWILIO_WHATSAPP_FROM).");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.twilio.com/2010-04-01/Accounts/{options.AccountSid}/Messages.json")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["From"] = $"whatsapp:{options.WhatsAppFrom}",
                ["To"] = $"whatsapp:{toE164}",
                ["Body"] = body,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.AccountSid}:{options.AuthToken}")));

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}

/// <summary>
/// Sender real de WhatsApp para los avisos de reservas: los de canal 'whatsapp' van por
/// Twilio y cualquier otro canal se delega en el sender interior (email real por SMTP o
/// simulado por log, según configuración).
/// </summary>
public class WhatsAppNotificationSender(
    IWhatsAppTransport transport,
    INotificationSender inner,
    ILogger<WhatsAppNotificationSender> logger) : INotificationSender
{
    public async Task SendAsync(Notification notification, CancellationToken ct = default)
    {
        if (notification.Channel != "whatsapp")
        {
            await inner.SendAsync(notification, ct);
            return;
        }

        await transport.SendAsync(notification.Recipient, notification.Body, ct);
        // Sin datos personales más allá del destinatario, como el resto del logging.
        logger.LogInformation(
            "[TWILIO:whatsapp] ({EventType}) → {Recipient}: enviado",
            notification.EventType, notification.Recipient);
    }
}
