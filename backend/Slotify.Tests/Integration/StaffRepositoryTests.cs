using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Infrastructure.Data;
using Slotify.Infrastructure.Repositories;

namespace Slotify.Tests.Integration;

/// <summary>
/// Listado de staff por negocio: solo trabajadores activos, con el owner primero
/// y el resto por antigüedad (fecha de alta).
/// </summary>
public class StaffRepositoryTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private SlotifyDbContext _db = null!;
    private StaffRepository _repo = null!;

    public StaffRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _db = _fixture.CreateContext();
        await _db.Database.MigrateAsync();
        _repo = new StaffRepository(_db);
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task ListByBusinessAsync_ReturnsActiveStaff_OwnerFirstThenByCreatedAt()
    {
        var business = await SeedBusinessAsync();
        var t0 = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        // El owner se llama "Zoe" (último alfabéticamente) y se crea el último, para
        // probar que va primero por ser owner, no por nombre ni por antigüedad.
        _db.Staff.AddRange(
            new Staff { Id = Guid.NewGuid(), BusinessId = business.Id, Role = "employee", Name = "Carlos", CreatedAt = t0.AddDays(1) },
            new Staff { Id = Guid.NewGuid(), BusinessId = business.Id, Role = "employee", Name = "Ana", CreatedAt = t0 },
            new Staff { Id = Guid.NewGuid(), BusinessId = business.Id, Role = "owner", Name = "Zoe", CreatedAt = t0.AddDays(2) });
        await _db.SaveChangesAsync();

        var list = await _repo.ListByBusinessAsync(business.Id);

        Assert.Equal(3, list.Count);
        Assert.Equal("Zoe", list[0].Name);    // owner primero
        Assert.Equal("Ana", list[1].Name);    // luego por antigüedad
        Assert.Equal("Carlos", list[2].Name);
    }

    [Fact]
    public async Task ListByBusinessAsync_ExcludesInactiveStaff()
    {
        var business = await SeedBusinessAsync();
        _db.Staff.AddRange(
            new Staff { Id = Guid.NewGuid(), BusinessId = business.Id, Role = "employee", Name = "Activa", Status = "active" },
            new Staff { Id = Guid.NewGuid(), BusinessId = business.Id, Role = "employee", Name = "Inactiva", Status = "inactive" });
        await _db.SaveChangesAsync();

        var list = await _repo.ListByBusinessAsync(business.Id);

        Assert.Single(list);
        Assert.Equal("Activa", list[0].Name);
    }

    [Fact]
    public async Task ListByBusinessAsync_OnlyStaffOfThatBusiness()
    {
        var businessA = await SeedBusinessAsync();
        var businessB = await SeedBusinessAsync();
        _db.Staff.AddRange(
            new Staff { Id = Guid.NewGuid(), BusinessId = businessA.Id, Role = "owner", Name = "De A" },
            new Staff { Id = Guid.NewGuid(), BusinessId = businessB.Id, Role = "owner", Name = "De B" });
        await _db.SaveChangesAsync();

        var list = await _repo.ListByBusinessAsync(businessA.Id);

        Assert.Single(list);
        Assert.Equal("De A", list[0].Name);
    }

    private async Task<Business> SeedBusinessAsync()
    {
        var free = await _db.PricingTiers.AsNoTracking().SingleAsync(t => t.Code == "free");
        var owner = new User
        {
            Id = Guid.NewGuid(),
            Email = $"owner-{Guid.NewGuid():N}@test.local",
            PasswordHash = "hash",
            Name = "Owner Test",
            Type = "owner",
        };
        var business = new Business
        {
            Id = Guid.NewGuid(),
            OwnerId = owner.Id,
            TierId = free.Id,
            Name = "Negocio Test",
        };
        _db.Users.Add(owner);
        _db.Businesses.Add(business);
        await _db.SaveChangesAsync();
        return business;
    }
}
