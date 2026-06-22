using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Reseñas de clientes. Un cliente registrado valora un negocio (1–5 + comentario) tras
/// haber tenido una reserva pasada allí: una reseña por (negocio, usuario), editable.
/// Al crear/editar se recalcula el agregado denormalizado del negocio (businesses.rating /
/// review_count) para que Explorar no haga joins.
/// </summary>
public class ReviewService(
    IReviewRepository reviews,
    IReservationRepository reservations,
    IBusinessRepository businesses)
{
    /// <summary>
    /// Crea (o actualiza, si ya existe) la reseña del usuario para el negocio de una
    /// reserva pasada suya, y recalcula la media del negocio.
    /// </summary>
    public async Task<ReviewResponse> CreateAsync(
        Guid reservationId, Guid currentUserId, int rating, string? comment, CancellationToken ct = default)
    {
        if (rating is < 1 or > 5)
            throw new InvalidReviewException("La valoración debe estar entre 1 y 5.");

        var reservation = await reservations.GetByIdAsync(reservationId, ct)
            ?? throw new ReservationNotFoundException(reservationId);

        // Solo el cliente registrado dueño de la reserva (los invitados no tienen cuenta).
        if (reservation.UserId is null || reservation.UserId != currentUserId)
            throw new ReviewForbiddenException();

        // Solo se valora lo que ya ha ocurrido.
        if (reservation.EndTime > DateTime.UtcNow)
            throw new ReviewNotAllowedException();

        var normalizedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

        // Una reseña por (negocio, usuario): si ya valoró el negocio, se edita.
        var review = await reviews.GetByBusinessAndUserAsync(reservation.BusinessId, currentUserId, ct);
        if (review is null)
        {
            review = new Review
            {
                Id = Guid.NewGuid(),
                BusinessId = reservation.BusinessId,
                UserId = currentUserId,
                ReservationId = reservationId,
                Rating = rating,
                Comment = normalizedComment,
                CreatedAt = DateTime.UtcNow,
            };
            await reviews.AddAsync(review, ct);
        }
        else
        {
            review.Rating = rating;
            review.Comment = normalizedComment;
            review.UpdatedAt = DateTime.UtcNow;
            await reviews.UpdateAsync(review, ct);
        }

        await RecomputeBusinessAggregateAsync(reservation.BusinessId, ct);

        return ReviewResponse.From(review);
    }

    /// <summary>Edita una reseña propia (desde "Mis reseñas") y recalcula la media del negocio.</summary>
    public async Task<MyReviewResponse> UpdateAsync(
        Guid reviewId, Guid currentUserId, int rating, string? comment, CancellationToken ct = default)
    {
        if (rating is < 1 or > 5)
            throw new InvalidReviewException("La valoración debe estar entre 1 y 5.");

        var review = await reviews.GetByIdAsync(reviewId, ct)
            ?? throw new ReviewNotFoundException(reviewId);
        if (review.UserId != currentUserId)
            throw new ReviewForbiddenException();

        review.Rating = rating;
        review.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        review.UpdatedAt = DateTime.UtcNow;
        await reviews.UpdateAsync(review, ct);

        await RecomputeBusinessAggregateAsync(review.BusinessId, ct);

        return MyReviewResponse.From(review);
    }

    /// <summary>Reseñas propias del cliente (para "Mis reseñas"), más recientes primero.</summary>
    public async Task<IReadOnlyList<MyReviewResponse>> ListMineAsync(Guid currentUserId, CancellationToken ct = default)
    {
        var list = await reviews.ListByUserAsync(currentUserId, ct);
        return list.Select(MyReviewResponse.From).ToList();
    }

    /// <summary>Reseñas públicas de un negocio (más recientes primero).</summary>
    public async Task<IReadOnlyList<ReviewResponse>> ListByBusinessAsync(Guid businessId, CancellationToken ct = default)
    {
        var list = await reviews.ListByBusinessAsync(businessId, ct);
        return list.Select(ReviewResponse.From).ToList();
    }

    /// <summary>Recalcula businesses.rating / review_count a partir de las reseñas existentes.</summary>
    private async Task RecomputeBusinessAggregateAsync(Guid businessId, CancellationToken ct)
    {
        var business = await businesses.GetByIdAsync(businessId, ct);
        if (business is null) return;

        var (count, average) = await reviews.GetBusinessAggregateAsync(businessId, ct);
        business.Rating = average is { } avg ? Math.Round(avg, 2) : null;
        business.ReviewCount = count;
        await businesses.UpdateAsync(business, ct);
    }
}
