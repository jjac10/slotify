using Slotify.Domain.Interfaces;

namespace Slotify.Infrastructure.Storage;

/// <summary>
/// Imágenes en disco local bajo {rootPath}/businesses, un fichero por negocio y slot
/// ('photo' | 'logo'; el nombre es {id}-{slot}: reemplazar no acumula huérfanos),
/// servidas por el backend como estáticos en /uploads (→ /api/uploads a través del
/// proxy de nginx/Vite). En producción {rootPath} vive en un volumen Docker
/// (sobrevive a los redeploys).
/// </summary>
public class LocalPhotoStorage(string rootPath) : IPhotoStorage
{
    private static readonly Dictionary<string, string> Extensions = new()
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp",
    };

    public async Task<string> SaveAsync(Guid businessId, string slot, Stream content, string contentType, CancellationToken ct = default)
    {
        var extension = Extensions[contentType.ToLowerInvariant()];
        var directory = Path.Combine(rootPath, "businesses");
        Directory.CreateDirectory(directory);

        // Reemplaza la imagen anterior del slot aunque cambiara de formato.
        foreach (var previous in Directory.GetFiles(directory, $"{businessId}-{slot}.*"))
            File.Delete(previous);

        var path = Path.Combine(directory, $"{businessId}-{slot}.{extension}");
        await using (var file = File.Create(path))
            await content.CopyToAsync(file, ct);

        // ?v= cambia en cada subida: el navegador no sirve la imagen vieja cacheada.
        return $"/api/uploads/businesses/{businessId}-{slot}.{extension}?v={DateTime.UtcNow.Ticks}";
    }
}
