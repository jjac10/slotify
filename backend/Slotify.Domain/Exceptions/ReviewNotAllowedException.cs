namespace Slotify.Domain.Exceptions;

/// <summary>Solo se puede valorar una reserva que ya ha ocurrido (pasada). HTTP 409.</summary>
public class ReviewNotAllowedException() : Exception("Solo puedes valorar una reserva una vez que ha ocurrido.");
