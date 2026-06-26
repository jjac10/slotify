using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

/// <summary>Implementación EF Core de <see cref="ITierRepository"/>.</summary>
public class TierRepository(SlotifyDbContext db) : ITierRepository
{
    public async Task<PricingTier> GetByBusinessAsync(Guid businessId, CancellationToken ct = default)
    {
        return await db.Businesses
            .Where(b => b.Id == businessId)
            .Select(b => b.Tier!)
            .AsNoTracking()
            .SingleAsync(ct);
    }

    public Task<PricingTier> GetByCodeAsync(string code, CancellationToken ct = default)
        => db.PricingTiers.AsNoTracking().SingleAsync(t => t.Code == code, ct);
}
