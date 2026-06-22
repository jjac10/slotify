namespace Slotify.Domain.Exceptions;

/// <summary>Solo el cliente registrado dueño de la reserva puede valorarla. HTTP 403.</summary>
public class ReviewForbiddenException() : Exception("Solo puedes valorar tus propias reservas.");
