using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>Acceso a la relación N:M trabajador↔servicio (staff_services).</summary>
public interface IStaffServiceRepository
{
    /// <summary>Ids de los servicios que tiene asignados un trabajador.</summary>
    Task<IReadOnlyList<Guid>> ListServiceIdsByStaffAsync(Guid staffId, CancellationToken ct = default);

    /// <summary>Reemplaza por completo las asignaciones de un trabajador.</summary>
    Task SetForStaffAsync(Guid staffId, IReadOnlyList<Guid> serviceIds, CancellationToken ct = default);

    /// <summary>
    /// Trabajadores activos del negocio que pueden realizar el servicio dado.
    /// Un trabajador SIN asignaciones se considera capaz de todos los servicios.
    /// </summary>
    Task<IReadOnlyList<Staff>> ListStaffByServiceAsync(Guid businessId, Guid serviceId, CancellationToken ct = default);
}
