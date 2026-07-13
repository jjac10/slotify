namespace Slotify.Domain.Interfaces;

/// <summary>
/// Avisa en tiempo real de cambios en reservas a quien esté conectado: el cliente de
/// la reserva (su canal de usuario) y el panel/agenda del negocio (su grupo). La
/// implementación real es SignalR en la capa API; el envío es best-effort y NUNCA
/// debe romper la operación que lo origina.
/// </summary>
public interface IRealtimeNotifier
{
    /// <param name="userId">Cliente registrado de la reserva, o null si es de invitado.</param>
    Task ReservationChangedAsync(
        Guid businessId, Guid? userId, Guid reservationId, string eventType, CancellationToken ct = default);
}
