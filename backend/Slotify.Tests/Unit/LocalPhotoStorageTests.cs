using Slotify.Infrastructure.Storage;

namespace Slotify.Tests.Unit;

/// <summary>
/// Almacenamiento local de imágenes: un fichero por negocio y slot ('photo'|'logo')
/// bajo {root}/businesses, servido por el backend en /uploads (→ /api/uploads desde
/// el frontend). Subir de nuevo al mismo slot reemplaza el fichero anterior aunque
/// cambie la extensión, y la URL lleva ?v= para que el navegador no sirva la versión
/// vieja cacheada.
/// </summary>
public class LocalPhotoStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"slotify-uploads-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static MemoryStream Bytes(params byte[] bytes) => new(bytes);

    [Fact]
    public async Task Save_WritesFile_AndReturnsPublicUrlWithCacheBuster()
    {
        var storage = new LocalPhotoStorage(_root);
        var businessId = Guid.NewGuid();

        var url = await storage.SaveAsync(businessId, "photo", Bytes(1, 2, 3), "image/png");

        Assert.StartsWith($"/api/uploads/businesses/{businessId}-photo.png?v=", url);
        var file = Path.Combine(_root, "businesses", $"{businessId}-photo.png");
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(file));
    }

    [Fact]
    public async Task Save_MapsContentTypeToExtension()
    {
        var storage = new LocalPhotoStorage(_root);
        var businessId = Guid.NewGuid();

        var url = await storage.SaveAsync(businessId, "photo", Bytes(1), "image/jpeg");

        Assert.Contains($"{businessId}-photo.jpg?v=", url);
    }

    [Fact]
    public async Task Save_ReplacesPreviousFileInSlot_EvenWithDifferentExtension()
    {
        var storage = new LocalPhotoStorage(_root);
        var businessId = Guid.NewGuid();

        await storage.SaveAsync(businessId, "photo", Bytes(1), "image/png");
        await storage.SaveAsync(businessId, "photo", Bytes(2), "image/jpeg");

        // Solo queda el .jpg: la foto vieja no se acumula como huérfana.
        var files = Directory.GetFiles(Path.Combine(_root, "businesses"), $"{businessId}-photo.*");
        Assert.Single(files);
        Assert.EndsWith(".jpg", files[0]);
    }

    [Fact]
    public async Task Save_PhotoAndLogo_Coexist()
    {
        var storage = new LocalPhotoStorage(_root);
        var businessId = Guid.NewGuid();

        await storage.SaveAsync(businessId, "photo", Bytes(1), "image/png");
        await storage.SaveAsync(businessId, "logo", Bytes(2), "image/png");

        var directory = Path.Combine(_root, "businesses");
        Assert.True(File.Exists(Path.Combine(directory, $"{businessId}-photo.png")));
        Assert.True(File.Exists(Path.Combine(directory, $"{businessId}-logo.png")));
    }

    [Fact]
    public async Task Save_KeepsOtherBusinessesFiles()
    {
        var storage = new LocalPhotoStorage(_root);
        var other = Guid.NewGuid();
        await storage.SaveAsync(other, "photo", Bytes(9), "image/png");

        await storage.SaveAsync(Guid.NewGuid(), "photo", Bytes(1), "image/png");

        Assert.True(File.Exists(Path.Combine(_root, "businesses", $"{other}-photo.png")));
    }
}
