namespace Slotify.Domain.Interfaces;

/// <summary>
/// Almacenamiento de la foto del negocio, swappable como el resto de la fontanería:
/// hoy disco local servido por el backend (/uploads), mañana un S3-compatible sin
/// tocar el dominio.
/// </summary>
public interface IPhotoStorage
{
    /// <summary>
    /// Guarda la imagen del negocio en su slot ('photo' | 'logo'), reemplazando la
    /// anterior si la hay, y devuelve la URL pública con la que el navegador puede
    /// pedirla.
    /// </summary>
    Task<string> SaveAsync(Guid businessId, string slot, Stream content, string contentType, CancellationToken ct = default);
}
