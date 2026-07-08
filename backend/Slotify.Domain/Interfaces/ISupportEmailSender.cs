namespace Slotify.Domain.Interfaces;

/// <summary>
/// "Envía" al equipo de Slotify los mensajes del formulario público de contacto/soporte.
/// En el TFM la implementación es simulada (registra en el log el mensaje); en producción
/// se sustituye por un proveedor real de email sin tocar los servicios de dominio (mismo
/// seam que <see cref="IAccountEmailSender"/>). El destinatario (email del dueño de la
/// plataforma) es responsabilidad de la implementación (configuración).
/// </summary>
public interface ISupportEmailSender
{
    /// <summary>Mensaje de contacto: nombre y email del remitente + texto libre.</summary>
    Task SendContactMessageAsync(string name, string email, string message, CancellationToken ct = default);
}
