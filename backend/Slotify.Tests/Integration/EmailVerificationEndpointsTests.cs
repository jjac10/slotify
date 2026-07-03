using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Slotify.API.Controllers;
using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;

namespace Slotify.Tests.Integration;

/// <summary>
/// Verificación de email NO bloqueante contra la API real. El token se lee del "email"
/// capturado por el sender de test (jamás viaja en la respuesta HTTP). Cubre: envío al
/// registrarse (customer y owner), registro que no falla aunque el envío falle, flujo
/// completo de verificación, token caducado/reusado/inválido, reenvío autenticado y
/// exposición de emailVerified en AuthResult y /auth/me.
/// </summary>
public class EmailVerificationEndpointsTests : IAsyncLifetime
{
    private readonly CapturingFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
    }

    Task IAsyncLifetime.DisposeAsync() => ((IAsyncLifetime)_factory).DisposeAsync();

    private static string UniqueEmail() => $"verify-{Guid.NewGuid():N}@test.local";

    private async Task<AuthResult> RegisterCustomerAsync(string email)
    {
        var response = await _client.PostAsJsonAsync(
            "/auth/register", new RegisterCustomerRequest(email, "SecurePass123!", "Ana"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResult>())!;
    }

    private string TokenFor(string email)
        => _factory.Emails.Verifications.Single(v => v.Email == email).Token;

    private static HttpRequestMessage Authenticated(HttpMethod method, string url, AuthResult session)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return message;
    }

    // --- Envío al registrarse ---

    [Fact]
    public async Task RegisterCustomer_SendsVerificationEmailAndNeverLeaksTokenInResponse()
    {
        var email = UniqueEmail();
        var register = await _client.PostAsJsonAsync(
            "/auth/register", new RegisterCustomerRequest(email, "SecurePass123!", "Ana"));

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var (recipient, token) = Assert.Single(_factory.Emails.Verifications.Where(v => v.Email == email));
        Assert.Equal(email, recipient);
        // El token viaja solo en el "email" simulado, nunca en la respuesta HTTP.
        var body = await register.Content.ReadAsStringAsync();
        Assert.DoesNotContain(token, body);

        // Y el registro deja la cuenta como no verificada (aviso en el frontend).
        var session = System.Text.Json.JsonSerializer.Deserialize<AuthResult>(
            body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.False(session!.EmailVerified);
    }

    [Fact]
    public async Task RegisterOwner_SendsVerificationEmail()
    {
        var email = UniqueEmail();
        var register = await _client.PostAsJsonAsync("/auth/register-owner",
            new RegisterOwnerRequest(email, "SecurePass123!", "Bea", "Peluquería Bea"));

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.Single(_factory.Emails.Verifications.Where(v => v.Email == email));
    }

    [Fact]
    public async Task Register_WhenEmailSendingFails_StillSucceeds()
    {
        _factory.Emails.FailEmailVerification = true;
        try
        {
            var email = UniqueEmail();
            var register = await _client.PostAsJsonAsync(
                "/auth/register", new RegisterCustomerRequest(email, "SecurePass123!", "Ana"));

            // NO bloqueante: el registro nunca falla porque falle el email.
            Assert.Equal(HttpStatusCode.Created, register.StatusCode);
            Assert.Empty(_factory.Emails.Verifications);
        }
        finally
        {
            _factory.Emails.FailEmailVerification = false;
        }
    }

    // --- POST /auth/verify-email ---

    [Fact]
    public async Task FullFlow_VerifyEmail_MarksVerifiedInMeAndNextLogin()
    {
        var email = UniqueEmail();
        var session = await RegisterCustomerAsync(email);

        // Antes de verificar: /auth/me lo refleja.
        var meBefore = await _client.SendAsync(Authenticated(HttpMethod.Get, "/auth/me", session));
        Assert.False((await meBefore.Content.ReadFromJsonAsync<MeResponse>())!.EmailVerified);

        // El usuario sigue el enlace del email (público, sin sesión).
        var verify = await _client.PostAsJsonAsync("/auth/verify-email", new VerifyEmailRequest(TokenFor(email)));
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        // /auth/me y el siguiente login reflejan la verificación.
        var meAfter = await _client.SendAsync(Authenticated(HttpMethod.Get, "/auth/me", session));
        Assert.True((await meAfter.Content.ReadFromJsonAsync<MeResponse>())!.EmailVerified);

        var login = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "SecurePass123!"));
        Assert.True((await login.Content.ReadFromJsonAsync<AuthResult>())!.EmailVerified);
    }

    [Fact]
    public async Task VerifyEmail_TokenIsSingleUse()
    {
        var email = UniqueEmail();
        await RegisterCustomerAsync(email);
        var token = TokenFor(email);

        (await _client.PostAsJsonAsync("/auth/verify-email", new VerifyEmailRequest(token)))
            .EnsureSuccessStatusCode();
        var second = await _client.PostAsJsonAsync("/auth/verify-email", new VerifyEmailRequest(token));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Contains("invalid_verification_token", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task VerifyEmail_InvalidToken_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync(
            "/auth/verify-email", new VerifyEmailRequest("token-invalido"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task VerifyEmail_ExpiredToken_ReturnsBadRequest()
    {
        var email = UniqueEmail();
        var session = await RegisterCustomerAsync(email);

        // Token caducado sembrado directamente (no se puede adelantar el reloj de la API).
        var expiredRaw = $"expired-{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IEmailVerificationTokenRepository>();
            await repo.AddAsync(new EmailVerificationToken
            {
                Id = Guid.NewGuid(),
                UserId = session.UserId,
                TokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(expiredRaw))),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            });
        }

        var response = await _client.PostAsJsonAsync("/auth/verify-email", new VerifyEmailRequest(expiredRaw));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- POST /auth/resend-verification ---

    [Fact]
    public async Task ResendVerification_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.PostAsync("/auth/resend-verification", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ResendVerification_GeneratesFreshTokenThatVerifies()
    {
        var email = UniqueEmail();
        var session = await RegisterCustomerAsync(email);
        var firstToken = TokenFor(email);

        var resend = await _client.SendAsync(Authenticated(HttpMethod.Post, "/auth/resend-verification", session));
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);

        var tokens = _factory.Emails.Verifications.Where(v => v.Email == email).Select(v => v.Token).ToList();
        Assert.Equal(2, tokens.Count);
        Assert.NotEqual(firstToken, tokens[1]); // reenvío = token regenerado

        // El token reenviado funciona.
        var verify = await _client.PostAsJsonAsync("/auth/verify-email", new VerifyEmailRequest(tokens[1]));
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
    }

    [Fact]
    public async Task ResendVerification_AlreadyVerified_ReturnsBadRequest()
    {
        var email = UniqueEmail();
        var session = await RegisterCustomerAsync(email);
        (await _client.PostAsJsonAsync("/auth/verify-email", new VerifyEmailRequest(TokenFor(email))))
            .EnsureSuccessStatusCode();

        var resend = await _client.SendAsync(Authenticated(HttpMethod.Post, "/auth/resend-verification", session));

        Assert.Equal(HttpStatusCode.BadRequest, resend.StatusCode);
        Assert.Contains("email_already_verified", await resend.Content.ReadAsStringAsync());
    }

    /// <summary>Factory que sustituye el sender simulado (logged) por el capturador.</summary>
    private sealed class CapturingFactory : SlotifyApiFactory
    {
        public CapturingAccountEmailSender Emails { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAccountEmailSender>();
                services.AddSingleton<IAccountEmailSender>(Emails);
            });
        }
    }
}
