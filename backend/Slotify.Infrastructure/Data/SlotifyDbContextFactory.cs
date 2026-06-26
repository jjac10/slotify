using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Slotify.Infrastructure.Data;

/// <summary>
/// Factory en tiempo de diseño: permite a `dotnet ef` crear el contexto sin un
/// proyecto de arranque (Infrastructure es una class library). La cadena de
/// conexión aquí es solo para que el proveedor Npgsql resuelva el modelo; no se
/// abre conexión al generar migraciones.
/// </summary>
public class SlotifyDbContextFactory : IDesignTimeDbContextFactory<SlotifyDbContext>
{
    public SlotifyDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SlotifyDbContext>()
            .UseNpgsql("Host=localhost;Database=slotify;Username=postgres;Password=postgres")
            .Options;
        return new SlotifyDbContext(options);
    }
}
