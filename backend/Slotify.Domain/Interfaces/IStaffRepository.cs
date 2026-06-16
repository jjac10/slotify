using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>Acceso a datos de staff.</summary>
public interface IStaffRepository
{
    /// <summary>Cuenta los trabajadores (incluido el owner) de un negocio.</summary>
    Task<int> CountByBusinessAsync(Guid businessId, CancellationToken ct = default);

    Task<Staff?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
