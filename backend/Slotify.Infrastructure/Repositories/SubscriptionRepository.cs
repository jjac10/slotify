using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

public class SubscriptionRepository(SlotifyDbContext db) : ISubscriptionRepository
{
    public async Task AddAsync(Subscription subscription, CancellationToken ct = default)
    {
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync(ct);
    }

    public Task<Subscription?> GetByExternalIdAsync(string externalId, CancellationToken ct = default)
        => db.Subscriptions.FirstOrDefaultAsync(s => s.ExternalId == externalId, ct);

    public Task<Subscription?> GetActiveByBusinessAsync(Guid businessId, CancellationToken ct = default)
        => db.Subscriptions
            .Where(s => s.BusinessId == businessId && s.Status == "active")
            .OrderByDescending(s => s.ActivatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task UpdateAsync(Subscription subscription, CancellationToken ct = default)
    {
        db.Subscriptions.Update(subscription);
        await db.SaveChangesAsync(ct);
    }
}
