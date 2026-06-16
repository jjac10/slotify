using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// Primer ciclo TDD de la capa de datos: seed de pricing_tiers y la relación
/// business → tier (NOT NULL). Schema canónico en docs/DATA_MODEL.md.
/// </summary>
public class PricingTierBusinessTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private SlotifyDbContext _db = null!;

    public PricingTierBusinessTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _db = _fixture.CreateContext();
        // Migrate (no EnsureCreated): aplica InitialCreate + el seed de la migración.
        await _db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task InitialMigration_SeedsFreeAndPremiumTiers()
    {
        var tiers = await _db.PricingTiers.AsNoTracking().ToListAsync();

        var free = Assert.Single(tiers, t => t.Code == "free");
        Assert.Equal(100, free.MaxReservationsPerMonth);
        Assert.Equal(50, free.MaxClients);
        Assert.Equal(5, free.MaxServices);
        Assert.Equal(1, free.MaxStaff);

        var premium = Assert.Single(tiers, t => t.Code == "premium");
        Assert.Null(premium.MaxReservationsPerMonth); // NULL = ilimitado
        Assert.Null(premium.MaxServices);
        Assert.Null(premium.MaxStaff);
        Assert.True(premium.HasApi);
    }

    [Fact]
    public async Task CreateBusiness_WithValidTier_Persists()
    {
        var owner = NewOwner();
        _db.Users.Add(owner);
        var free = await _db.PricingTiers.AsNoTracking().SingleAsync(t => t.Code == "free");

        var business = new Business
        {
            Id = Guid.NewGuid(),
            OwnerId = owner.Id,
            TierId = free.Id,
            Name = "Barbería Test",
        };
        _db.Businesses.Add(business);
        await _db.SaveChangesAsync();

        var loaded = await _db.Businesses.AsNoTracking().SingleAsync(b => b.Id == business.Id);
        Assert.Equal(free.Id, loaded.TierId);
        Assert.Equal(owner.Id, loaded.OwnerId);
    }

    [Fact]
    public async Task CreateBusiness_WithoutValidTier_IsRejected()
    {
        var owner = NewOwner();
        _db.Users.Add(owner);
        await _db.SaveChangesAsync();

        // tier_id es NOT NULL + FK: un tier inexistente debe romper el guardado.
        var business = new Business
        {
            Id = Guid.NewGuid(),
            OwnerId = owner.Id,
            TierId = Guid.NewGuid(), // no existe en pricing_tiers
            Name = "Negocio sin plan válido",
        };
        _db.Businesses.Add(business);

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    private static User NewOwner() => new()
    {
        Id = Guid.NewGuid(),
        Email = $"owner-{Guid.NewGuid():N}@test.local",
        PasswordHash = "hash",
        Name = "Owner Test",
        Type = "owner",
    };
}
