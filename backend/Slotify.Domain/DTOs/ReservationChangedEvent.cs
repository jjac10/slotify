namespace Slotify.Domain.DTOs;

/// <summary>
/// Evento en tiempo real "reservationChanged" (SignalR): algo cambió en una reserva
/// (created | confirmed | cancelled | rescheduled | no-show). Solo ids y tipo — los
/// interesados recargan sus listados por la API normal (sin datos personales en el hub).
/// </summary>
public record ReservationChangedEvent(Guid ReservationId, Guid BusinessId, string EventType);
