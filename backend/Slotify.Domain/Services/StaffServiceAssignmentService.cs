using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Asignación de servicios a trabajadores (qué puede realizar cada staff). Solo el
/// owner gestiona; valida que trabajador y servicios pertenecen al negocio. Lista
/// vacía = el trabajador puede realizar todos los servicios.
/// </summary>
public class StaffServiceAssignmentService(
    IStaffServiceRepository assignments,
    IStaffRepository staff,
    IServiceRepository services,
    IBusinessRepository businesses)
{
    /// <summary>Ids de los servicios asignados a un trabajador (solo el owner).</summary>
    public async Task<IReadOnlyList<Guid>> ListAsync(
        Guid businessId, Guid staffId, Guid ownerId, CancellationToken ct = default)
    {
        await EnsureOwnerAndStaffAsync(businessId, staffId, ownerId, ct);
        return await assignments.ListServiceIdsByStaffAsync(staffId, ct);
    }

    /// <summary>Reemplaza los servicios asignados a un trabajador (solo el owner).</summary>
    public async Task<IReadOnlyList<Guid>> SetAsync(
        Guid businessId, Guid staffId, Guid ownerId, IReadOnlyList<Guid> serviceIds, CancellationToken ct = default)
    {
        await EnsureOwnerAndStaffAsync(businessId, staffId, ownerId, ct);

        var requested = serviceIds.Distinct().ToList();
        if (requested.Count > 0)
        {
            var businessServiceIds = (await services.ListByBusinessAsync(businessId, ct))
                .Select(s => s.Id)
                .ToHashSet();
            foreach (var serviceId in requested)
                if (!businessServiceIds.Contains(serviceId))
                    throw new ServiceNotFoundException(serviceId);
        }

        await assignments.SetForStaffAsync(staffId, requested, ct);
        return requested;
    }

    private async Task EnsureOwnerAndStaffAsync(Guid businessId, Guid staffId, Guid ownerId, CancellationToken ct)
    {
        var business = await businesses.GetByIdAsync(businessId, ct)
            ?? throw new BusinessNotFoundException(businessId);
        if (business.OwnerId != ownerId)
            throw new NotBusinessOwnerException();

        var member = await staff.GetByIdAsync(staffId, ct);
        if (member is null || member.BusinessId != businessId)
            throw new StaffNotFoundException(staffId);
    }
}
