using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

/// <summary>Implementación EF Core de <see cref="IAuthRepository"/>.</summary>
public class AuthRepository(SlotifyDbContext db) : IAuthRepository
{
    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => db.Users.AnyAsync(u => u.Email == email, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task AddUserAsync(User user, CancellationToken ct = default)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
    }

    public async Task RegisterOwnerAsync(User user, Business business, Staff ownerStaff, IEnumerable<BusinessHour> hours, CancellationToken ct = default)
    {
        db.Users.Add(user);
        db.Businesses.Add(business);
        db.Staff.Add(ownerStaff);
        db.BusinessHours.AddRange(hours);
        // Un único SaveChanges => transacción atómica.
        await db.SaveChangesAsync(ct);
    }
}
