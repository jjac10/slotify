using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>Acceso a datos del histórico de notificaciones.</summary>
public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken ct = default);

    /// <summary>¿Ya se envió un aviso de ese tipo para esa reserva? (evita recordatorios duplicados).</summary>
    Task<bool> ExistsForReservationAsync(Guid reservationId, string eventType, CancellationToken ct = default);
}
