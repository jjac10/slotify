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

    /// <summary>Trabajador por token de invitación pendiente (para que el empleado cree su cuenta), o null.</summary>
    Task<Staff?> GetByInviteTokenAsync(string token, CancellationToken ct = default);

    /// <summary>El registro de staff enlazado a un usuario (su pertenencia a un negocio), o null.</summary>
    Task<Staff?> GetByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Da de alta un trabajador.</summary>
    Task AddAsync(Staff staff, CancellationToken ct = default);

    /// <summary>Persiste cambios sobre un trabajador (edición o baja lógica).</summary>
    Task UpdateAsync(Staff staff, CancellationToken ct = default);

    /// <summary>¿Es el usuario miembro del staff del negocio? (para autorización por rol)</summary>
    Task<bool> ExistsForUserAsync(Guid userId, Guid businessId, CancellationToken ct = default);
}
