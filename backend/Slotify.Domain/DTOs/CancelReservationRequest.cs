namespace Slotify.Domain.DTOs;

/// <summary>
/// Cancelación de una reserva (POST /reservations/{id}/cancel). <paramref name="Reason"/>
/// es el motivo opcional; <paramref name="Contact"/> + <paramref name="OtpCode"/> solo los
/// usa el invitado (sin JWT) para verificar identidad (código pedido en
/// POST /reservations/lookup/otp). Todo va en el body — el contacto es dato personal.
/// </summary>
public record CancelReservationRequest(string? Reason = null, string? Contact = null, string? OtpCode = null);
