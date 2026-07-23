using Slotify.Infrastructure.Storage;

namespace Slotify.Tests.Unit;

/// <summary>
/// Almacenamiento local de fotos: un fichero por negocio bajo {root}/businesses,
/// servido por el backend en /uploads (→ /api/uploads desde el frontend). Subir de
/// nuevo reemplaza la foto anterior aunque cambie la extensión, y la URL lleva ?v=
/// para que el navegador no sirva la versión vieja cacheada.
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

        var url = await storage.SaveAsync(businessId, Bytes(1, 2, 3), "image/png");

        Assert.StartsWith($"/api/uploads/businesses/{businessId}.png?v=", url);
        var file = Path.Combine(_root, "businesses", $"{businessId}.png");
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(file));
    }

    [Fact]
    public async Task Save_MapsContentTypeToExtension()
    {
        var storage = new LocalPhotoStorage(_root);
        var businessId = Guid.NewGuid();

        var url = await storage.SaveAsync(businessId, Bytes(1), "image/jpeg");

        Assert.Contains($"{businessId}.jpg?v=", url);
    }

    [Fact]
    public async Task Save_ReplacesPreviousPhoto_EvenWithDifferentExtension()
    {
        var storage = new LocalPhotoStorage(_root);
        var businessId = Guid.NewGuid();

        await storage.SaveAsync(businessId, Bytes(1), "image/png");
        await storage.SaveAsync(businessId, Bytes(2), "image/jpeg");

        // Solo queda el .jpg: la foto vieja no se acumula como huérfana.
        var files = Directory.GetFiles(Path.Combine(_root, "businesses"), $"{businessId}.*");
        Assert.Single(files);
        Assert.EndsWith(".jpg", files[0]);
    }

    [Fact]
    public async Task Save_KeepsOtherBusinessesPhotos()
    {
        var storage = new LocalPhotoStorage(_root);
        var other = Guid.NewGuid();
        await storage.SaveAsync(other, Bytes(9), "image/png");

        await storage.SaveAsync(Guid.NewGuid(), Bytes(1), "image/png");

        Assert.True(File.Exists(Path.Combine(_root, "businesses", $"{other}.png")));
    }
}
