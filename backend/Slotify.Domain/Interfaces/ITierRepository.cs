using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>Acceso al plan (tier) de un negocio vía businesses.tier_id.</summary>
public interface ITierRepository
{
    /// <summary>Devuelve el tier del negocio indicado.</summary>
    Task<PricingTier> GetByBusinessAsync(Guid businessId, CancellationToken ct = default);

    /// <summary>Devuelve el tier por su código estable ('free', 'premium').</summary>
    Task<PricingTier> GetByCodeAsync(string code, CancellationToken ct = default);
}
