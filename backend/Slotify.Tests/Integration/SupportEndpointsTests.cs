using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Slotify.Domain.DTOs;
using Slotify.Domain.Interfaces;

namespace Slotify.Tests.Integration;

/// <summary>
/// Formulario público de contacto/soporte contra la API real. El "email" simulado se
/// captura con un sender de test (mismo patrón que PasswordResetEndpointsTests): el
/// test verifica qué se "envió", sin proveedor real de email.
/// </summary>
public class SupportEndpointsTests : IAsyncLifetime
{
    private readonly CapturingFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
    }

    Task IAsyncLifetime.DisposeAsync() => ((IAsyncLifetime)_factory).DisposeAsync();

    private Task<HttpResponseMessage> PostContactAsync(string? name, string? email, string? message) =>
        _client.PostAsJsonAsync("/support/contact", new ContactSupportRequest(name, email, message));

    // --- Camino feliz ---

    [Fact]
    public async Task Contact_ValidRequest_Returns204AndSendsMessage()
    {
        var response = await PostContactAsync(
            "Ana García", "ana@example.com", "Hola, tengo una duda sobre las reservas.");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var sent = Assert.Single(_factory.Emails.ContactMessages);
        Assert.Equal("Ana García", sent.Name);
        Assert.Equal("ana@example.com", sent.Email);
        Assert.Equal("Hola, tengo una duda sobre las reservas.", sent.Message);
    }

    [Fact]
    public async Task Contact_IsPublic_NoAuthenticationRequired()
    {
        // Sin cabecera Authorization: el endpoint es [AllowAnonymous].
        var response = await PostContactAsync("Ana", "ana@example.com", "Mensaje sin sesión.");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Contact_TrimsFieldsBeforeSending()
    {
        var response = await PostContactAsync("  Ana  ", " ana@example.com ", "  Con espacios.  ");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var sent = Assert.Single(_factory.Emails.ContactMessages);
        Assert.Equal(("Ana", "ana@example.com", "Con espacios."), sent);
    }

    // --- Validación → 400 con la forma de error estándar, sin "enviar" nada ---

    [Theory]
    [InlineData(null, "ana@example.com", "Hola.")]
    [InlineData("   ", "ana@example.com", "Hola.")]
    [InlineData("Ana", null, "Hola.")]
    [InlineData("Ana", "no-es-un-email", "Hola.")]
    [InlineData("Ana", "ana@example.com", null)]
    [InlineData("Ana", "ana@example.com", "   ")]
    public async Task Contact_InvalidFields_Returns400AndNeverSends(string? name, string? email, string? message)
    {
        var response = await PostContactAsync(name, email, message);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid_contact_message", body);
        Assert.Empty(_factory.Emails.ContactMessages);
    }

    [Fact]
    public async Task Contact_MessageTooLong_Returns400AndNeverSends()
    {
        var response = await PostContactAsync("Ana", "ana@example.com", new string('m', 2001));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid_contact_message", body);
        Assert.Empty(_factory.Emails.ContactMessages);
    }

    /// <summary>Factory que sustituye el sender simulado (logged) por el capturador.</summary>
    private sealed class CapturingFactory : SlotifyApiFactory
    {
        public CapturingSupportEmailSender Emails { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISupportEmailSender>();
                services.AddSingleton<ISupportEmailSender>(Emails);
            });
        }
    }
}

/// <summary>
/// Rate limiting anti-spam de /support/contact (misma política "auth" por IP que
/// login/register). Clase separada con su propia factory de límite bajo, igual que
/// RateLimitingTests: el contador del fixed window vive en el servidor.
/// </summary>
public class SupportRateLimitingTests : IAsyncLifetime
{
    private const int PermitLimit = 3;

    private readonly RateLimitedApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
    }

    Task IAsyncLifetime.DisposeAsync() => ((IAsyncLifetime)_factory).DisposeAsync();

    private Task<HttpResponseMessage> PostContactAsync() =>
        _client.PostAsJsonAsync("/support/contact",
            new ContactSupportRequest("Ana", "ana@example.com", "Mensaje de prueba."));

    [Fact]
    public async Task Contact_UpToLimit_IsNotRateLimited()
    {
        for (var i = 0; i < PermitLimit; i++)
        {
            var response = await PostContactAsync();
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }

    [Fact]
    public async Task Contact_BeyondLimit_Returns429WithRetryAfter()
    {
        for (var i = 0; i < PermitLimit; i++)
            (await PostContactAsync()).Dispose();

        var rejected = await PostContactAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.Contains("Retry-After"),
            "la respuesta 429 debe incluir la cabecera Retry-After");
    }

    /// <summary>Factory con el límite de la política "auth" bajado explícitamente.</summary>
    private sealed class RateLimitedApiFactory : SlotifyApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:AuthPermitLimit"] = PermitLimit.ToString(),
                    ["RateLimiting:AuthWindowSeconds"] = "60",
                });
            });
        }
    }
}
