namespace Slotify.Domain.DTOs;

/// <summary>
/// Cambia el modo de reservas del negocio: <c>online</c> (los clientes reservan por
/// internet, sale en Explorar) o <c>calendar_only</c> (solo el owner apunta reservas
/// desde la Agenda). Solo el owner.
/// </summary>
public record SetBookingModeRequest(string Mode);
