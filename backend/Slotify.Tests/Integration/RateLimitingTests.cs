using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Slotify.Domain.DTOs;

namespace Slotify.Tests.Integration;

/// <summary>
/// Rate limiting de los endpoints de autenticación (política "auth", fixed window por IP).
/// Cada test usa su propia factory con un límite bajo (3 peticiones/60s) para poder
/// agotarlo sin interferir con el resto de la suite ni entre tests (el contador del
/// fixed window vive en el servidor, así que cada test arranca una API limpia).
/// </summary>
public class RateLimitingTests : IAsyncLifetime
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

    private Task<HttpResponseMessage> PostLoginAsync() =>
        _client.PostAsJsonAsync("/auth/login", new LoginRequest("noexiste@test.local", "WrongPass123!"));

    private Task<HttpResponseMessage> PostRegisterAsync() =>
        _client.PostAsJsonAsync("/auth/register",
            new RegisterCustomerRequest($"rl-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Ana"));

    [Fact]
    public async Task Login_UpToLimit_IsNotRateLimited()
    {
        for (var i = 0; i < PermitLimit; i++)
        {
            var response = await PostLoginAsync();

            // Credenciales inválidas → 401, pero nunca 429 dentro del límite.
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task Login_BeyondLimit_Returns429WithRetryAfter()
    {
        for (var i = 0; i < PermitLimit; i++)
            (await PostLoginAsync()).Dispose();

        var rejected = await PostLoginAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.Contains("Retry-After"),
            "la respuesta 429 debe incluir la cabecera Retry-After");
    }

    [Fact]
    public async Task Register_BeyondLimit_Returns429()
    {
        for (var i = 0; i < PermitLimit; i++)
        {
            var response = await PostRegisterAsync();
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        var rejected = await PostRegisterAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    /// <summary>
    /// Factory con el límite de auth bajado explícitamente. La factory base
    /// (SlotifyApiFactory) usa un límite alto para no romper la suite existente.
    /// </summary>
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
