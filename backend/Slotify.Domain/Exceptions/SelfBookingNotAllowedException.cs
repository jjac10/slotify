namespace Slotify.Domain.Exceptions;

/// <summary>El usuario autenticado es el mismo trabajador asignado a la reserva. HTTP 400.</summary>
public class SelfBookingNotAllowedException()
    : Exception("No puedes reservar contigo mismo como trabajador.");
