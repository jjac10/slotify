namespace Slotify.Domain.Exceptions;

/// <summary>Solo se puede marcar la no asistencia de una cita que ya empezó. HTTP 409.</summary>
public class ReservationNotPastException()
    : Exception("La cita aún no ha empezado: no se puede marcar como no asistida.");
