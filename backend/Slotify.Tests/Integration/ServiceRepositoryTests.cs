using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Infrastructure.Data;
using Slotify.Infrastructure.Repositories;

namespace Slotify.Tests.Integration;

/// <summary>Capa de datos de `services`: alta, listado y conteo por negocio.</summary>
public class ServiceRepositoryTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private SlotifyDbContext _db = null!;

    public ServiceRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _db = _fixture.CreateContext();
        await _db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task Add_ThenListAndCount_ReturnsService()
    {
        var business = await SeedBusinessAsync();
        var repo = new ServiceRepository(_db);

        await repo.AddAsync(new Service
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            Name = "Corte de cabello",
            DurationMinutes = 30,
            Price = 15.00m,
        });

        var list = await repo.ListByBusinessAsync(business.Id);
        var count = await repo.CountByBusinessAsync(business.Id);

        Assert.Single(list);
        Assert.Equal("Corte de cabello", list[0].Name);
        Assert.Equal(30, list[0].DurationMinutes);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Add_WithoutValidBusiness_IsRejected()
    {
        var repo = new ServiceRepository(_db);

        var service = new Service
        {
            Id = Guid.NewGuid(),
            BusinessId = Guid.NewGuid(), // no existe
            Name = "Huérfano",
            DurationMinutes = 30,
        };

        await Assert.ThrowsAsync<DbUpdateException>(() => repo.AddAsync(service));
    }

    private async Task<Business> SeedBusinessAsync()
    {
        var owner = new User
        {
            Id = Guid.NewGuid(),
            Email = $"owner-{Guid.NewGuid():N}@test.local",
            PasswordHash = "hash",
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
