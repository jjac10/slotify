namespace Slotify.Domain.DTOs;

/// <summary>
/// Búsqueda de reservas de un invitado por su teléfono o email (POST /reservations/lookup).
/// El contacto viaja en el body (no en la URL) por ser dato personal: así no queda en
/// logs, historial del navegador ni cabeceras Referer.
/// </summary>
public record LookupGuestReservationsRequest(string? Contact);
