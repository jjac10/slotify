namespace Slotify.Domain.Exceptions;

/// <summary>Solo se puede confirmar una reserva que está pendiente (estado 'pending').</summary>
public class ReservationNotPendingException(string status)
    : Exception($"La reserva no se puede confirmar porque su estado es '{status}', no 'pending'.");
