using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Reseñas de clientes. Un cliente registrado valora (1–5 + comentario) una reserva
/// pasada SUYA, una sola vez. Al crear la reseña se recalcula el agregado denormalizado
/// del negocio (businesses.rating / review_count) para que Explorar no haga joins.
/// </summary>
public class ReviewService(
    IReviewRepository reviews,
    IReservationRepository reservations,
    IBusinessRepository businesses)
{
    /// <summary>Crea la reseña de una reserva pasada del usuario y actualiza la media del negocio.</summary>
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

        if (await reviews.ExistsForReservationAsync(reservationId, ct))
            throw new AlreadyReviewedException();

        var review = new Review
        {
            Id = Guid.NewGuid(),
            BusinessId = reservation.BusinessId,
            UserId = currentUserId,
            ReservationId = reservationId,
            Rating = rating,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        await reviews.AddAsync(review, ct);

        await RecomputeBusinessAggregateAsync(reservation.BusinessId, ct);

        return ReviewResponse.From(review);
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
