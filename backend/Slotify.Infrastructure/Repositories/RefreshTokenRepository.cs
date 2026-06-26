using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;
using Slotify.Infrastructure.Security;

namespace Slotify.Infrastructure.Repositories;

/// <summary>
/// Implementación EF Core de <see cref="IRefreshTokenRepository"/>. Guarda el
/// token hasheado (SHA-256) y aplica rotación (un solo uso) al consumir.
/// </summary>
public class RefreshTokenRepository(SlotifyDbContext db, JwtOptions options) : IRefreshTokenRepository
{
    public async Task IssueAsync(Guid userId, string token, CancellationToken ct = default)
    {
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(token),
            ExpiresAt = DateTime.UtcNow.AddDays(options.RefreshTokenDays),
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid?> ConsumeAsync(string token, CancellationToken ct = default)
    {
        var hash = Hash(token);
        var entity = await db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash, ct);
        if (entity is null || entity.ExpiresAt <= DateTime.UtcNow)
            return null;

        db.RefreshTokens.Remove(entity); // rotación: un refresh token solo se usa una vez
        await db.SaveChangesAsync(ct);
        return entity.UserId;
    }

    private static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
