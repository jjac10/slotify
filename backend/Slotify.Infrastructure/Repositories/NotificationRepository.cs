using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

/// <summary>Implementación EF Core de <see cref="INotificationRepository"/>.</summary>
public class NotificationRepository(SlotifyDbContext db) : INotificationRepository
{
    public async Task AddAsync(Notification notification, CancellationToken ct = default)
    {
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);
    }

    public Task<bool> ExistsForReservationAsync(Guid reservationId, string eventType, CancellationToken ct = default)
        => db.Notifications.AnyAsync(n => n.ReservationId == reservationId && n.EventType == eventType, ct);
}
