using Microsoft.Extensions.Logging;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;

namespace Slotify.Infrastructure.Notifications;

/// <summary>
/// Sender de notificaciones "simulado" para el TFM: no envía de verdad, registra el aviso
/// en el log (y el <c>NotificationService</c> lo persiste con estado 'logged'). Es el seam
/// para conectar un proveedor real de email/WhatsApp sin tocar la lógica de despacho.
/// </summary>
public class LoggedNotificationSender(ILogger<LoggedNotificationSender> logger) : INotificationSender
{
    public Task SendAsync(Notification notification, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[NOTIFY:{Channel}] ({EventType}) → {Recipient}: {Body}",
            notification.Channel, notification.EventType, notification.Recipient, notification.Body);
        return Task.CompletedTask;
    }
}
