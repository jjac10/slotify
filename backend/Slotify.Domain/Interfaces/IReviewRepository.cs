using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>Acceso a datos de reseñas (Repository Pattern, ADR #2).</summary>
public interface IReviewRepository
{
    Task AddAsync(Review review, CancellationToken ct = default);

    Task UpdateAsync(Review review, CancellationToken ct = default);

    /// <summary>Reseña por id (con el negocio cargado), o null.</summary>
    Task<Review?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>La reseña del usuario para ese negocio (una por negocio/usuario), o null.</summary>
    Task<Review?> GetByBusinessAndUserAsync(Guid businessId, Guid userId, CancellationToken ct = default);

    /// <summary>Reseñas de un negocio, con su autor, de más reciente a más antigua.</summary>
    Task<IReadOnlyList<Review>> ListByBusinessAsync(Guid businessId, CancellationToken ct = default);

    /// <summary>Reseñas de un usuario, con el negocio cargado, de más reciente a más antigua.</summary>
    Task<IReadOnlyList<Review>> ListByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Agregado denormalizable de un negocio: número de reseñas y media de la nota
    /// (null si no hay ninguna). Para recalcular businesses.rating / review_count.
    /// </summary>
    Task<(int Count, double? Average)> GetBusinessAggregateAsync(Guid businessId, CancellationToken ct = default);
}
