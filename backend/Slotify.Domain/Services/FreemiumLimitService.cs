using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Valida los límites del plan (Freemium) de forma data-driven: el límite se lee
/// del tier del negocio (ADR #9). NULL en un límite = ilimitado.
/// </summary>
public class FreemiumLimitService(ITierRepository tiers, IStaffRepository staff, IServiceRepository services)
{
    /// <summary>¿Puede el negocio añadir un trabajador más según su plan?</summary>
    public async Task<bool> CanAddStaffAsync(Guid businessId, CancellationToken ct = default)
    {
        var tier = await tiers.GetByBusinessAsync(businessId, ct);
        if (tier.MaxStaff is null)
            return true; // ilimitado: no hace falta contar

        var current = await staff.CountByBusinessAsync(businessId, ct);
        return current < tier.MaxStaff;
    }

    /// <summary>¿Puede el negocio añadir un servicio más según su plan?</summary>
    public async Task<bool> CanAddServiceAsync(Guid businessId, CancellationToken ct = default)
    {
        var tier = await tiers.GetByBusinessAsync(businessId, ct);
        if (tier.MaxServices is null)
            return true; // ilimitado

        var current = await services.CountByBusinessAsync(businessId, ct);
        return current < tier.MaxServices;
    }
}
