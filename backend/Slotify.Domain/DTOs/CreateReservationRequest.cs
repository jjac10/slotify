namespace Slotify.Domain.DTOs;

/// <summary>
/// Datos para crear una reserva (API.md POST /reservations). Si la petición está
/// autenticada, el cliente es el usuario del JWT; si no, es un invitado y debe
/// venir <see cref="GuestName"/> + exactamente uno de teléfono/email.
/// </summary>
public record CreateReservationRequest(
    Guid BusinessId,
    Guid ServiceId,
    Guid StaffId,
    DateTime StartTime,
    string? GuestName,
    string? GuestPhone,
    string? GuestEmail);
