using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Infrastructure.Data;
using Slotify.Infrastructure.Repositories;

namespace Slotify.Tests.Integration;

/// <summary>
/// El repositorio persiste negocio + owner-staff de forma atómica contra
/// PostgreSQL real. Cierra el flujo owner-as-staff junto a BusinessServiceTests.
/// </summary>
public class BusinessRepositoryTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private SlotifyDbContext _db = null!;

    public BusinessRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _db = _fixture.CreateContext();
        await _db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task AddWithOwnerStaffAsync_PersistsBusinessAndOwnerStaff()
    {
        var owner = new User
        {
            Id = Guid.NewGuid(),
            Email = $"owner-{Guid.NewGuid():N}@test.local",
            PasswordHash = "hash",
            Name = "Pepe",
            Type = "owner",
        };
        var free = await _db.PricingTiers.AsNoTracking().SingleAsync(t => t.Code == "free");
        _db.Users.Add(owner);
        await _db.SaveChangesAsync();

        var business = new Business
        {
            Id = Guid.NewGuid(),
            OwnerId = owner.Id,
            TierId = free.Id,
            Name = "Barbería Pepe",
        };
        var ownerStaff = new Staff
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            UserId = owner.Id,
            Role = "owner",
            Name = owner.Name,
        };

        var repo = new BusinessRepository(_db);
        await repo.AddWithOwnerStaffAsync(business, ownerStaff);

        // Contexto nuevo: comprobamos que realmente está en la BD.
        await using var verify = _fixture.CreateContext();
        var loadedBusiness = await verify.Businesses.AsNoTracking().SingleAsync(b => b.Id == business.Id);
        var loadedOwnerStaff = await verify.Staff.AsNoTracking()
            .SingleAsync(s => s.BusinessId == business.Id && s.Role == "owner");

        Assert.Equal(owner.Id, loadedBusiness.OwnerId);
        Assert.Equal(owner.Id, loadedOwnerStaff.UserId);
        Assert.Equal(business.Id, loadedOwnerStaff.BusinessId);
    }

    [Fact]
    public async Task SearchPublicAsync_FiltersByNameCaseInsensitive()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        await SeedBusinessAsync($"Barbería {token}");
        await SeedBusinessAsync($"Spa {token}");
        var repo = new BusinessRepository(_db);

        // Sin filtro: ambos están presentes (el token es único de este test).
        var byToken = await repo.SearchPublicAsync(token);
        Assert.Equal(2, byToken.Count(b => b.Name.Contains(token)));

        // Filtro por nombre en MAYÚSCULAS → ILIKE insensible a mayúsculas.
        var byName = await repo.SearchPublicAsync($"BARBERÍA {token}");
        Assert.Single(byName.Where(b => b.Name.Contains(token)));
        Assert.Contains(byName, b => b.Name == $"Barbería {token}");
    }

    private async Task SeedBusinessAsync(string name)
    {
        var owner = new User
        {
            Id = Guid.NewGuid(),
            Email = $"o-{Guid.NewGuid():N}@test.local",
            PasswordHash = "h",
            Name = "O",
            Type = "owner",
        };
        var free = await _db.PricingTiers.AsNoTracking().SingleAsync(t => t.Code == "free");
        _db.Users.Add(owner);
        await _db.SaveChangesAsync();

        var business = new Business { Id = Guid.NewGuid(), OwnerId = owner.Id, TierId = free.Id, Name = name };
        var staff = new Staff { Id = Guid.NewGuid(), BusinessId = business.Id, UserId = owner.Id, Role = "owner", Name = "O" };
        await new BusinessRepository(_db).AddWithOwnerStaffAsync(business, staff);
    }
}
