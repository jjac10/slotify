using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

/// <summary>Implementación EF Core de <see cref="IReviewRepository"/>.</summary>
public class ReviewRepository(SlotifyDbContext db) : IReviewRepository
{
    public async Task AddAsync(Review review, CancellationToken ct = default)
    {
        db.Reviews.Add(review);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Review review, CancellationToken ct = default)
    {
        db.Reviews.Update(review);
        await db.SaveChangesAsync(ct);
    }

    public Task<Review?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Reviews.Include(r => r.Business).FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<Review?> GetByBusinessAndUserAsync(Guid businessId, Guid userId, CancellationToken ct = default)
        => db.Reviews.FirstOrDefaultAsync(r => r.BusinessId == businessId && r.UserId == userId, ct);

    public async Task<IReadOnlyList<Review>> ListByBusinessAsync(Guid businessId, CancellationToken ct = default)
        => await db.Reviews.AsNoTracking()
            .Include(r => r.User)
            .Where(r => r.BusinessId == businessId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Review>> ListByUserAsync(Guid userId, CancellationToken ct = default)
        => await db.Reviews.AsNoTracking()
            .Include(r => r.Business)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public async Task<(int Count, double? Average)> GetBusinessAggregateAsync(Guid businessId, CancellationToken ct = default)
    {
        var count = await db.Reviews.CountAsync(r => r.BusinessId == businessId, ct);
        if (count == 0)
            return (0, null);

        var average = await db.Reviews
            .Where(r => r.BusinessId == businessId)
            .AverageAsync(r => (double)r.Rating, ct);
        return (count, average);
    }
}
