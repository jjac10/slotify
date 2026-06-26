namespace Slotify.Domain.Exceptions;

/// <summary>No existe la reserva indicada. HTTP 404.</summary>
public class ReservationNotFoundException(Guid reservationId)
    : Exception($"No existe la reserva '{reservationId}'.");
