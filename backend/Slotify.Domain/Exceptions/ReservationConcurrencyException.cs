namespace Slotify.Domain.Exceptions;

/// <summary>
/// La reserva fue modificada por otra operación entre la lectura y la escritura
/// (optimistic locking: la versión no coincide). HTTP 409.
/// </summary>
public class ReservationConcurrencyException()
    : Exception("La reserva fue modificada por otra operación; vuelve a intentarlo.");
