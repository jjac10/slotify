using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;
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

    public async Task<User?> FindActiveUserByContactAsync(string? normalizedEmail, string? normalizedPhone, CancellationToken ct = default)
    {
        if (normalizedEmail is not null)
            // El email es UNIQUE; comparamos en minúsculas (NormalizeEmail ya lo deja así).
            return await db.Users.FirstOrDefaultAsync(u => u.Status == "active" && u.Email.ToLower() == normalizedEmail, ct);

        if (normalizedPhone is not null)
        {
            // El teléfono se guarda tal cual se registró; lo normalizamos en memoria para
            // comparar de forma robusta (formatos/espacios). Escala pequeña (negocio local).
            var candidates = await db.Users.AsNoTracking()
                .Where(u => u.Status == "active" && u.Phone != null)
                .ToListAsync(ct);
            return candidates.FirstOrDefault(u => ContactNormalizer.NormalizePhone(u.Phone!) == normalizedPhone);
        }

        return null;
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
