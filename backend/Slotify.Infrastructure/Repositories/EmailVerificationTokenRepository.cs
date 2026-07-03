using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

/// <summary>Implementación EF Core de <see cref="IEmailVerificationTokenRepository"/>.</summary>
public class EmailVerificationTokenRepository(SlotifyDbContext db) : IEmailVerificationTokenRepository
{
    public async Task AddAsync(EmailVerificationToken token, CancellationToken ct = default)
    {
        db.EmailVerificationTokens.Add(token);
        await db.SaveChangesAsync(ct);
    }

    public Task<EmailVerificationToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        => db.EmailVerificationTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task UpdateAsync(EmailVerificationToken token, CancellationToken ct = default)
    {
        db.EmailVerificationTokens.Update(token);
        await db.SaveChangesAsync(ct);
    }
}
