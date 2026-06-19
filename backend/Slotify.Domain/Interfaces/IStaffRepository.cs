using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>Acceso a datos de staff.</summary>
public interface IStaffRepository
{
    /// <summary>Cuenta los trabajadores activos (incluido el owner) de un negocio (para límites Freemium).</summary>
    Task<int> CountByBusinessAsync(Guid businessId, CancellationToken ct = default);

    /// <summary>Trabajadores activos de un negocio, ordenados por nombre (listado público).</summary>
    Task<IReadOnlyList<Staff>> ListByBusinessAsync(Guid businessId, CancellationToken ct = default);

    Task<Staff?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Da de alta un trabajador.</summary>
    Task AddAsync(Staff staff, CancellationToken ct = default);

    /// <summary>Persiste cambios sobre un trabajador (edición o baja lógica).</summary>
    Task UpdateAsync(Staff staff, CancellationToken ct = default);

    /// <summary>¿Es el usuario miembro del staff del negocio? (para autorización por rol)</summary>
    Task<bool> ExistsForUserAsync(Guid userId, Guid businessId, CancellationToken ct = default);
}
