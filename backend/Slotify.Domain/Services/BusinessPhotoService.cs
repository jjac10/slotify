using Slotify.Domain.DTOs;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Subida de la foto del negocio (solo el owner): valida tipo y tamaño, la guarda
/// vía <see cref="IPhotoStorage"/> y deja la URL pública en photo_url. Pegar una URL
/// externa en el perfil sigue funcionando; esto es el camino sin depender de nadie.
/// </summary>
public class BusinessPhotoService(IBusinessRepository businesses, IPhotoStorage storage)
{
    public const long MaxBytes = 5 * 1024 * 1024;

    // Solo formatos de imagen inofensivos: SVG queda fuera (puede llevar scripts).
    private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];

    public async Task<BusinessResponse> UploadAsync(
        Guid businessId, Guid userId, Stream content, string contentType, long length, CancellationToken ct = default)
    {
        var normalizedType = contentType?.ToLowerInvariant();
        if (normalizedType is null || !AllowedContentTypes.Contains(normalizedType))
            throw new InvalidPhotoException("Formato no soportado: usa una imagen JPG, PNG o WebP.");
        if (length is <= 0 or > MaxBytes)
            throw new InvalidPhotoException("La imagen no puede superar los 5 MB.");

        var business = await businesses.GetByIdAsync(businessId, ct)
            ?? throw new BusinessNotFoundException(businessId);
        if (business.OwnerId != userId)
            throw new NotBusinessOwnerException();

        business.PhotoUrl = await storage.SaveAsync(businessId, content, normalizedType, ct);
        await businesses.UpdateAsync(business, ct);

        return BusinessResponse.From(business);
    }
}
