using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Slotify.Domain.DTOs;
using Slotify.Domain.Interfaces;

namespace Slotify.Tests.Integration;

/// <summary>
/// Flujo de recuperación de contraseña contra la API real. El "email" simulado se
/// captura con un sender de test (el token JAMÁS viaja en la respuesta HTTP: el test
/// lo lee del email capturado, igual que haría un usuario real desde su bandeja).
/// </summary>
public class PasswordResetEndpointsTests : IAsyncLifetime
{
    private readonly CapturingFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
    }

    Task IAsyncLifetime.DisposeAsync() => ((IAsyncLifetime)_factory).DisposeAsync();

    private async Task<string> RegisterCustomerAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync(
            "/auth/register", new RegisterCustomerRequest(email, password, "Ana"));
        response.EnsureSuccessStatusCode();
        return email;
    }

    private static string UniqueEmail() => $"reset-{Guid.NewGuid():N}@test.local";

    [Fact]
    public async Task ForgotPassword_UnknownEmail_Returns200Generic()
    {
        var response = await _client.PostAsJsonAsync(
            "/auth/forgot-password", new ForgotPasswordRequest("nadie@test.local"));

        // Anti-enumeración: 200 genérico aunque el email no exista.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("instrucciones", body, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_factory.Emails.PasswordResets); // y no se "envía" nada
    }

    [Fact]
    public async Task ForgotPassword_KnownEmail_Returns200AndNeverLeaksTokenInResponse()
    {
        var email = await RegisterCustomerAsync(UniqueEmail(), "SecurePass123!");

        var response = await _client.PostAsJsonAsync("/auth/forgot-password", new ForgotPasswordRequest(email));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (recipient, token) = Assert.Single(_factory.Emails.PasswordResets.Where(s => s.Email == email));
        Assert.Equal(email, recipient);
        // El token viaja solo en el "email" simulado, nunca en la respuesta HTTP.
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(token, body);
    }

    [Fact]
    public async Task FullFlow_ResetPassword_AllowsLoginWithNewPasswordOnly()
    {
        var email = await RegisterCustomerAsync(UniqueEmail(), "OldSecure123!");

        (await _client.PostAsJsonAsync("/auth/forgot-password", new ForgotPasswordRequest(email)))
            .EnsureSuccessStatusCode();
        var token = _factory.Emails.PasswordResets.Single(s => s.Email == email).Token;

        var reset = await _client.PostAsJsonAsync(
            "/auth/reset-password", new ResetPasswordRequest(token, "NewSecure456!"));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        // La contraseña antigua deja de valer; la nueva funciona.
        var oldLogin = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "OldSecure123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        var newLogin = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(email, "NewSecure456!"));
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_TokenIsSingleUse()
    {
        var email = await RegisterCustomerAsync(UniqueEmail(), "OldSecure123!");
        (await _client.PostAsJsonAsync("/auth/forgot-password", new ForgotPasswordRequest(email)))
            .EnsureSuccessStatusCode();
        var token = _factory.Emails.PasswordResets.Single(s => s.Email == email).Token;

        (await _client.PostAsJsonAsync("/auth/reset-password", new ResetPasswordRequest(token, "NewSecure456!")))
            .EnsureSuccessStatusCode();
        var second = await _client.PostAsJsonAsync(
            "/auth/reset-password", new ResetPasswordRequest(token, "OtherSecure789!"));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync(
            "/auth/reset-password", new ResetPasswordRequest("token-invalido", "NewSecure456!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WeakPassword_ReturnsBadRequest()
    {
        var email = await RegisterCustomerAsync(UniqueEmail(), "OldSecure123!");
        (await _client.PostAsJsonAsync("/auth/forgot-password", new ForgotPasswordRequest(email)))
            .EnsureSuccessStatusCode();
        var token = _factory.Emails.PasswordResets.Single(s => s.Email == email).Token;

        var response = await _client.PostAsJsonAsync(
            "/auth/reset-password", new ResetPasswordRequest(token, "weak"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_RevokesActiveRefreshTokens()
    {
        var email = UniqueEmail();
        var register = await _client.PostAsJsonAsync(
            "/auth/register", new RegisterCustomerRequest(email, "OldSecure123!", "Ana"));
        var session = await register.Content.ReadFromJsonAsync<AuthResult>();

        (await _client.PostAsJsonAsync("/auth/forgot-password", new ForgotPasswordRequest(email)))
            .EnsureSuccessStatusCode();
        var token = _factory.Emails.PasswordResets.Single(s => s.Email == email).Token;
        (await _client.PostAsJsonAsync("/auth/reset-password", new ResetPasswordRequest(token, "NewSecure456!")))
            .EnsureSuccessStatusCode();

        // El refresh token emitido antes del reset queda invalidado (cierre de sesiones).
        var refresh = await _client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(session!.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
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
