using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Slotify.Tests.Integration;

/// <summary>
/// Arranca la API real (WebApplicationFactory) apuntando a un PostgreSQL en Docker
/// (Testcontainers). La migración al arranque crea el schema en ese contenedor.
/// </summary>
public class SlotifyApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _container.GetConnectionString(),
                // Límite de rate limiting muy alto por defecto: la suite hace login/register
                // decenas de veces desde la misma "IP". Los tests de rate limiting lo bajan
                // explícitamente en su propia factory (RateLimitingTests).
                ["RateLimiting:AuthPermitLimit"] = "100000",
                ["RateLimiting:AuthWindowSeconds"] = "60",
            });
        });
    }
}
