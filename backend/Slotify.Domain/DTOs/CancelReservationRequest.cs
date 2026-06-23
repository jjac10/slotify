namespace Slotify.Domain.DTOs;

/// <summary>
/// Cancelación de una reserva (POST /reservations/{id}/cancel). <paramref name="Reason"/>
/// es el motivo opcional; <paramref name="Contact"/> solo lo usa el invitado (sin JWT) para
/// verificar identidad. Ambos van en el body (no en la URL) — el contacto es dato personal.
/// </summary>
public record CancelReservationRequest(string? Reason = null, string? Contact = null);
