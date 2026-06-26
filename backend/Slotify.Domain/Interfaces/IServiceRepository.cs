using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>Acceso a datos de servicios.</summary>
public interface IServiceRepository
{
    Task AddAsync(Service service, CancellationToken ct = default);

    /// <summary>Persiste cambios sobre un servicio (edición o archivado).</summary>
    Task UpdateAsync(Service service, CancellationToken ct = default);

    Task<Service?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Service>> ListByBusinessAsync(Guid businessId, CancellationToken ct = default);

    /// <summary>Cuenta los servicios activos de un negocio (para límites Freemium).</summary>
    Task<int> CountByBusinessAsync(Guid businessId, CancellationToken ct = default);
}
