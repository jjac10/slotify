using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Infrastructure.Data;
using Slotify.Infrastructure.Repositories;

namespace Slotify.Tests.Integration;

/// <summary>
/// Listado de staff por negocio: solo trabajadores activos, ordenados por nombre.
/// (Mismo criterio que <see cref="ServiceRepository"/> para servicios.)
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
    public async Task ListByBusinessAsync_ReturnsActiveStaff_OrderedByName()
    {
        var business = await SeedBusinessAsync();
        _db.Staff.AddRange(
            new Staff { Id = Guid.NewGuid(), BusinessId = business.Id, Role = "employee", Name = "Zoe" },
            new Staff { Id = Guid.NewGuid(), BusinessId = business.Id, Role = "owner", Name = "Ana" });
        await _db.SaveChangesAsync();

        var list = await _repo.ListByBusinessAsync(business.Id);

        Assert.Equal(2, list.Count);
        Assert.Equal("Ana", list[0].Name);
        Assert.Equal("Zoe", list[1].Name);
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
