using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Infrastructure.Data;
using Slotify.Infrastructure.Repositories;

namespace Slotify.Tests.Integration;

/// <summary>
/// Capa de datos de `guests`: persistencia con cifrado + blind index, dedupe por
/// (business_id, *_hash) y CHECK de "al menos un contacto". Schema: DATA_MODEL.md.
/// </summary>
public class GuestRepositoryTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private SlotifyDbContext _db = null!;

    public GuestRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _db = _fixture.CreateContext();
        await _db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task Add_WithPhone_PersistsAndIsFoundByHash()
    {
        var business = await SeedBusinessAsync();
        var repo = new GuestRepository(_db);

        var guest = new Guest
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            Name = "Juan",
            PhoneEncrypted = "AES(...)",
            PhoneHash = "phone-hash-1",
        };
        await repo.AddAsync(guest);

        var found = await repo.FindByHashAsync(business.Id, "phone-hash-1", null);
        Assert.NotNull(found);
        Assert.Equal(guest.Id, found!.Id);
    }

    [Fact]
    public async Task Add_DuplicatePhoneHashInSameBusiness_IsRejected()
    {
        var business = await SeedBusinessAsync();
        var repo = new GuestRepository(_db);
        await repo.AddAsync(new Guest { Id = Guid.NewGuid(), BusinessId = business.Id, Name = "A", PhoneHash = "dup" });

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            repo.AddAsync(new Guest { Id = Guid.NewGuid(), BusinessId = business.Id, Name = "B", PhoneHash = "dup" }));
    }

    [Fact]
    public async Task Add_WithoutAnyContactHash_IsRejected()
    {
        var business = await SeedBusinessAsync();
        var repo = new GuestRepository(_db);

        // CHECK phone_hash IS NOT NULL OR email_hash IS NOT NULL
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            repo.AddAsync(new Guest { Id = Guid.NewGuid(), BusinessId = business.Id, Name = "Sin contacto" }));
    }

    private async Task<Business> SeedBusinessAsync()
    {
        var owner = new User
        {
            Id = Guid.NewGuid(),
            Email = $"owner-{Guid.NewGuid():N}@test.local",
            PasswordHash = "h",
            Name = "Owner",
            Type = "owner",
        };
        var free = await _db.PricingTiers.AsNoTracking().SingleAsync(t => t.Code == "free");
        var business = new Business { Id = Guid.NewGuid(), OwnerId = owner.Id, TierId = free.Id, Name = "Negocio" };
        _db.Users.Add(owner);
        _db.Businesses.Add(business);
        await _db.SaveChangesAsync();
        return business;
    }
}
