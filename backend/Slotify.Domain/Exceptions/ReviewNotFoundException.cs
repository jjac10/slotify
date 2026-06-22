namespace Slotify.Domain.Exceptions;

/// <summary>La reseña indicada no existe. HTTP 404.</summary>
public class ReviewNotFoundException(Guid reviewId) : Exception($"No existe la reseña {reviewId}.");
