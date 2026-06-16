using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Services;
using Slotify.Infrastructure.Data;
using Slotify.Infrastructure.Repositories;

namespace Slotify.Tests.Integration;

/// <summary>
/// Caso real de límites Freemium contra Postgres: un negocio Free nace con su
/// owner-staff, que ya ocupa el único hueco (MaxStaff=1); Premium es ilimitado.
/// </summary>
public class FreemiumLimitIntegrationTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private SlotifyDbContext _db = null!;

    public FreemiumLimitIntegrationTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _db = _fixture.CreateContext();
        await _db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task CanAddStaff_FreeBusinessWithOwnerStaff_ReturnsFalse()
    {
        var business = await SeedBusinessWithOwnerAsync("free");
        var service = new FreemiumLimitService(new TierRepository(_db), new StaffRepository(_db), new ServiceRepository(_db));

        Assert.False(await service.CanAddStaffAsync(business.Id));
    }

    [Fact]
    public async Task CanAddStaff_PremiumBusiness_ReturnsTrue()
    {
        var business = await SeedBusinessWithOwnerAsync("premium");
        var service = new FreemiumLimitService(new TierRepository(_db), new StaffRepository(_db), new ServiceRepository(_db));

        Assert.True(await service.CanAddStaffAsync(business.Id));
    }

    private async Task<Business> SeedBusinessWithOwnerAsync(string tierCode)
    {
        var owner = new User
        {
            Id = Guid.NewGuid(),
            Email = $"owner-{Guid.NewGuid():N}@test.local",
            PasswordHash = "hash",
            Name = "Owner Test",
            Type = "owner",
        };
        var tier = await _db.PricingTiers.AsNoTracking().SingleAsync(t => t.Code == tierCode);
        _db.Users.Add(owner);
        await _db.SaveChangesAsync();

        var business = new Business
        {
            Id = Guid.NewGuid(),
            OwnerId = owner.Id,
            TierId = tier.Id,
            Name = "Negocio Test",
        };
        var ownerStaff = new Staff
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            UserId = owner.Id,
            Role = "owner",
            Name = owner.Name,
        };
        await new BusinessRepository(_db).AddWithOwnerStaffAsync(business, ownerStaff);
        return business;
    }
}
