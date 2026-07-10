namespace Slotify.Domain.DTOs;

/// <summary>
/// Datos para reprogramar una reserva (API.md PATCH /reservations/{id}). Solo cambia
/// el inicio; el fin se recalcula conservando la duración. La autorización (owner del
/// negocio, staff o el propio usuario) y el anti-doble-booking se aplican en servidor.
/// <paramref name="Contact"/> + <paramref name="OtpCode"/> solo los usa el invitado (sin
/// JWT) para verificar identidad (código pedido en POST /reservations/lookup/otp).
/// </summary>
public record RescheduleReservationRequest(DateTime StartTime, string? Contact = null, string? OtpCode = null);
