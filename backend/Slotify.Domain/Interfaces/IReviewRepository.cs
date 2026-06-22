using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>Acceso a datos de reseñas (Repository Pattern, ADR #2).</summary>
public interface IReviewRepository
{
    Task AddAsync(Review review, CancellationToken ct = default);

    /// <summary>¿Ya existe una reseña para esa reserva? (una reseña por reserva).</summary>
    Task<bool> ExistsForReservationAsync(Guid reservationId, CancellationToken ct = default);

    /// <summary>Reseñas de un negocio, con su autor, de más reciente a más antigua.</summary>
    Task<IReadOnlyList<Review>> ListByBusinessAsync(Guid businessId, CancellationToken ct = default);

    /// <summary>
    /// Agregado denormalizable de un negocio: número de reseñas y media de la nota
    /// (null si no hay ninguna). Para recalcular businesses.rating / review_count.
    /// </summary>
    Task<(int Count, double? Average)> GetBusinessAggregateAsync(Guid businessId, CancellationToken ct = default);
}
