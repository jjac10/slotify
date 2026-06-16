using Microsoft.EntityFrameworkCore;
using Slotify.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace Slotify.Tests.Integration;

/// <summary>
/// Levanta un PostgreSQL real en Docker (Testcontainers) para los tests de
/// integración de la capa de datos. Un contenedor por clase de test.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Crea un DbContext apuntando al contenedor.</summary>
    public SlotifyDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SlotifyDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new SlotifyDbContext(options);
    }
}
