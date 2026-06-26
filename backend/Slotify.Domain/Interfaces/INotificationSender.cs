using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>
/// Envía un aviso por su canal. En el TFM la implementación es "logged" (simula el
/// envío y lo registra); en producción se sustituye por email/WhatsApp reales sin
/// tocar la lógica de <c>NotificationService</c> (ADR: seam de proveedor).
/// </summary>
public interface INotificationSender
{
    Task SendAsync(Notification notification, CancellationToken ct = default);
}
