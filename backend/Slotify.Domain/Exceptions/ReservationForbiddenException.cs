namespace Slotify.Domain.Exceptions;

/// <summary>El usuario no puede gestionar esta reserva (no es owner/staff ni su dueño). HTTP 403.</summary>
public class ReservationForbiddenException() : Exception("No tienes permisos sobre esta reserva.");
