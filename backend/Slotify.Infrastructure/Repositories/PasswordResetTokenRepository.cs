using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

/// <summary>Implementación EF Core de <see cref="IPasswordResetTokenRepository"/>.</summary>
public class PasswordResetTokenRepository(SlotifyDbContext db) : IPasswordResetTokenRepository
{
    public async Task AddAsync(PasswordResetToken token, CancellationToken ct = default)
    {
        db.PasswordResetTokens.Add(token);
        await db.SaveChangesAsync(ct);
    }

    public Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        => db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task UpdateAsync(PasswordResetToken token, CancellationToken ct = default)
    {
        db.PasswordResetTokens.Update(token);
        await db.SaveChangesAsync(ct);
    }
}
