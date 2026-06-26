namespace Slotify.Domain.Exceptions;

/// <summary>Una reserva solo se puede valorar una vez (unique reservation_id). HTTP 409.</summary>
public class AlreadyReviewedException() : Exception("Esta reserva ya tiene una reseña.");
