using Slotify.Domain.Entities;

namespace Slotify.Domain.DTOs;

/// <summary>Alta de una reseña (POST /reservations/{id}/review): nota 1–5 + comentario opcional.</summary>
public record CreateReviewRequest(int Rating, string? Comment = null);

/// <summary>Representación de una reseña para la API. <c>AuthorName</c> se rellena si la consulta cargó el usuario.</summary>
public record ReviewResponse(
    Guid Id,
    Guid BusinessId,
    Guid ReservationId,
    int Rating,
    string? Comment,
    string? AuthorName,
    DateTime CreatedAt)
{
    public static ReviewResponse From(Review r) =>
        new(r.Id, r.BusinessId, r.ReservationId, r.Rating, r.Comment, r.User?.Name, r.CreatedAt);
}
