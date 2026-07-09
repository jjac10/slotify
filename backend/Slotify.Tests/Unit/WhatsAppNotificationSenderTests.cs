using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Notifications;

namespace Slotify.Tests.Unit;

/// <summary>
/// Envío real de WhatsApp vía Twilio: <see cref="WhatsAppNotificationSender"/> manda los
/// avisos de canal 'whatsapp' por <see cref="IWhatsAppTransport"/> y delega cualquier otro
/// canal en el sender interior (email real o simulado). <see cref="TwilioOptions"/> decide
/// si hay configuración (fallback a simulado si no) y <see cref="TwilioWhatsAppTransport"/>
/// construye la petición HTTP correcta contra la API de Twilio.
/// </summary>
public class WhatsAppNotificationSenderTests
{
    private readonly Mock<IWhatsAppTransport> _transport = new();
    private readonly Mock<INotificationSender> _inner = new();

    private WhatsAppNotificationSender CreateSender() =>
        new(_transport.Object, _inner.Object, NullLogger<WhatsAppNotificationSender>.Instance);

    private static Notification Notification(string channel, string recipient) => new()
    {
        Id = Guid.NewGuid(),
        BusinessId = Guid.NewGuid(),
        ReservationId = Guid.NewGuid(),
        Channel = channel,
        EventType = "created",
        Recipient = recipient,
        Body = "Tu reserva en Barbería Elite es el 10/07 a las 16:30.",
    };

    [Fact]
    public async Task SendAsync_WhatsappChannel_SendsViaTransportWithRecipientAndBody()
    {
        await CreateSender().SendAsync(Notification("whatsapp", "+34600111222"));

        _transport.Verify(t => t.SendAsync(
            "+34600111222", "Tu reserva en Barbería Elite es el 10/07 a las 16:30.",
            It.IsAny<CancellationToken>()), Times.Once);
        _inner.Verify(i => i.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_EmailChannel_DelegatesToInnerSender()
    {
        var notification = Notification("email", "cliente@example.com");

        await CreateSender().SendAsync(notification);

        _inner.Verify(i => i.SendAsync(notification, It.IsAny<CancellationToken>()), Times.Once);
        _transport.Verify(t => t.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

/// <summary>TWILIO_ACCOUNT_SID/AUTH_TOKEN/WHATSAPP_FROM desde el entorno.</summary>
public class TwilioOptionsTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
            .Build();

    [Fact]
    public void FromConfiguration_AllVariables_IsConfigured()
    {
        var options = TwilioOptions.FromConfiguration(Config(
            ("TWILIO_ACCOUNT_SID", "ACxxxx"),
            ("TWILIO_AUTH_TOKEN", "token"),
            ("TWILIO_WHATSAPP_FROM", "+14155238886")));

        Assert.True(options.IsConfigured);
        Assert.Equal("ACxxxx", options.AccountSid);
        Assert.Equal("token", options.AuthToken);
        Assert.Equal("+14155238886", options.WhatsAppFrom);
    }

    [Theory]
    [InlineData("TWILIO_ACCOUNT_SID")]
    [InlineData("TWILIO_AUTH_TOKEN")]
    [InlineData("TWILIO_WHATSAPP_FROM")]
    public void FromConfiguration_MissingAnyVariable_IsNotConfigured(string missingKey)
    {
        var all = new List<(string, string)>
        {
            ("TWILIO_ACCOUNT_SID", "ACxxxx"), ("TWILIO_AUTH_TOKEN", "token"), ("TWILIO_WHATSAPP_FROM", "+14155238886"),
        };
        all.RemoveAll(v => v.Item1 == missingKey);

        Assert.False(TwilioOptions.FromConfiguration(Config(all.ToArray())).IsConfigured);
    }
}

/// <summary>La petición HTTP a Twilio: URL de la cuenta, Basic auth y campos del form.</summary>
public class TwilioWhatsAppTransportTests
{
    /// <summary>Handler falso que captura la petición y devuelve 201 (como Twilio).</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request;
        public string? FormBody;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            FormBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.Created);
        }
    }

    private static readonly TwilioOptions Options = new()
    {
        AccountSid = "AC123",
        AuthToken = "secreto",
        WhatsAppFrom = "+14155238886",
    };

    [Fact]
    public async Task SendAsync_PostsToTwilioMessagesEndpointWithBasicAuthAndWhatsappFields()
    {
        var handler = new CapturingHandler();
        var transport = new TwilioWhatsAppTransport(new HttpClient(handler), Options);

        await transport.SendAsync("+34600111222", "Hola desde Slotify");

        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.twilio.com/2010-04-01/Accounts/AC123/Messages.json", handler.Request.RequestUri!.ToString());

        var expectedAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes("AC123:secreto"));
        Assert.Equal("Basic", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal(expectedAuth, handler.Request.Headers.Authorization.Parameter);

        // Form URL-encoded con el prefijo whatsapp: en origen y destino
        Assert.Contains("From=whatsapp%3A%2B14155238886", handler.FormBody);
        Assert.Contains("To=whatsapp%3A%2B34600111222", handler.FormBody);
        Assert.Contains("Body=Hola+desde+Slotify", handler.FormBody);
    }

    [Fact]
    public async Task SendAsync_TwilioRejects_Throws()
    {
        var handler = new RejectingHandler();
        var transport = new TwilioWhatsAppTransport(new HttpClient(handler), Options);

        await Assert.ThrowsAsync<HttpRequestException>(() => transport.SendAsync("+34600111222", "Hola"));
    }

    private sealed class RejectingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
    }
}
