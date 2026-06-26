using Slotify.Domain.Entities;

namespace Slotify.Domain.DTOs;

/// <summary>Alta/edición de una reseña (POST /reservations/{id}/review): nota 1–5 + comentario opcional.</summary>
public record CreateReviewRequest(int Rating, string? Comment = null);

/// <summary>Edición de una reseña (PUT /reviews/{id}): nota 1–5 + comentario opcional.</summary>
public record UpdateReviewRequest(int Rating, string? Comment = null);

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

/// <summary>Reseña propia del cliente (pantalla "Mis reseñas"), con el nombre del negocio.</summary>
public record MyReviewResponse(
    Guid Id,
    Guid BusinessId,
    string BusinessName,
    int Rating,
    string? Comment,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static MyReviewResponse From(Review r) =>
        new(r.Id, r.BusinessId, r.Business?.Name ?? "Negocio", r.Rating, r.Comment, r.CreatedAt, r.UpdatedAt);
}
