using Microsoft.AspNetCore.SignalR;
using Slotify.Domain.DTOs;
using Slotify.Domain.Interfaces;

namespace Slotify.API.Realtime;

/// <summary>
/// Implementación SignalR de <see cref="IRealtimeNotifier"/>: emite
/// "reservationChanged" al canal del cliente de la reserva y al grupo del negocio.
/// Best-effort: cualquier fallo se loguea y jamás rompe la operación que lo origina.
/// </summary>
public class SignalRRealtimeNotifier(
    IHubContext<ReservationsHub> hub,
    ILogger<SignalRRealtimeNotifier> logger) : IRealtimeNotifier
{
    public async Task ReservationChangedAsync(
        Guid businessId, Guid? userId, Guid reservationId, string eventType, CancellationToken ct = default)
    {
        var payload = new ReservationChangedEvent(reservationId, businessId, eventType);
        try
        {
            await hub.Clients.Group(ReservationsHub.BusinessGroup(businessId))
                .SendAsync("reservationChanged", payload, ct);
            if (userId is { } uid)
                await hub.Clients.Group(ReservationsHub.UserGroup(uid))
                    .SendAsync("reservationChanged", payload, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo emitir el evento en tiempo real ({EventType}, {ReservationId})",
                eventType, reservationId);
        }
    }
}
