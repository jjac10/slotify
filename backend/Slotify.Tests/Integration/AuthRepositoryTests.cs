using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Infrastructure.Data;
using Slotify.Infrastructure.Repositories;
using Slotify.Infrastructure.Security;

namespace Slotify.Tests.Integration;

/// <summary>
/// Repos EF de autenticación contra Postgres: alta atómica de owner y ciclo de
/// vida del refresh token (emitir → consumir una sola vez).
/// </summary>
public class AuthRepositoryTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private SlotifyDbContext _db = null!;

    public AuthRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _db = _fixture.CreateContext();
        await _db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task RegisterOwnerAsync_PersistsUserBusinessAndStaff()
    {
        var free = await _db.PricingTiers.AsNoTracking().SingleAsync(t => t.Code == "free");
        var email = $"owner-{Guid.NewGuid():N}@test.local";
        var user = new User { Id = Guid.NewGuid(), Email = email, PasswordHash = "h", Name = "Pepe", Type = "owner" };
        var business = new Business { Id = Guid.NewGuid(), OwnerId = user.Id, TierId = free.Id, Name = "Barbería Pepe" };
        var staff = new Staff { Id = Guid.NewGuid(), BusinessId = business.Id, UserId = user.Id, Role = "owner", Name = "Pepe" };

        var repo = new AuthRepository(_db);
        await repo.RegisterOwnerAsync(user, business, staff);

        Assert.True(await repo.EmailExistsAsync(email));
        var loaded = await repo.GetByEmailAsync(email);
        Assert.NotNull(loaded);
        Assert.Equal(user.Id, loaded!.Id);

        await using var verify = _fixture.CreateContext();
        Assert.True(await verify.Businesses.AnyAsync(b => b.Id == business.Id));
        Assert.True(await verify.Staff.AnyAsync(s => s.BusinessId == business.Id && s.Role == "owner"));
    }

    [Fact]
    public async Task RefreshToken_IssueThenConsume_IsSingleUse()
    {
        var user = new User { Id = Guid.NewGuid(), Email = $"u-{Guid.NewGuid():N}@test.local", PasswordHash = "h", Name = "U" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var options = new JwtOptions { Key = "k", Issuer = "i", Audience = "a", RefreshTokenDays = 7 };
        var repo = new RefreshTokenRepository(_db, options);
        const string token = "the-refresh-token-value";

        await repo.IssueAsync(user.Id, token);

        var first = await repo.ConsumeAsync(token);
        var second = await repo.ConsumeAsync(token);

        Assert.Equal(user.Id, first);
        Assert.Null(second); // rotación: un refresh token solo se usa una vez
    }
}
