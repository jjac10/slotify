using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Infrastructure.Data;
using Slotify.Infrastructure.Repositories;

namespace Slotify.Tests.Integration;

/// <summary>Capa de datos de `business_holidays`: alta, listado, UNIQUE por fecha y borrado.</summary>
public class BusinessHolidayRepositoryTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private SlotifyDbContext _db = null!;

    public BusinessHolidayRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _db = _fixture.CreateContext();
        await _db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    private static readonly DateOnly Christmas = new(2026, 12, 25);

    [Fact]
    public async Task Add_ThenList_ReturnsHoliday()
    {
        var business = await SeedBusinessAsync();
        var repo = new BusinessHolidayRepository(_db);

        await repo.AddAsync(new BusinessHoliday
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            HolidayDate = Christmas,
            Reason = "Navidad",
        });

        var list = await repo.ListByBusinessAsync(business.Id);
        Assert.Single(list);
        Assert.Equal(Christmas, list[0].HolidayDate);
        Assert.True(list[0].IsClosed);
    }

    [Fact]
    public async Task Add_DuplicateDate_IsRejected()
    {
        var business = await SeedBusinessAsync();
        var repo = new BusinessHolidayRepository(_db);
        await repo.AddAsync(new BusinessHoliday { Id = Guid.NewGuid(), BusinessId = business.Id, HolidayDate = Christmas });

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            repo.AddAsync(new BusinessHoliday { Id = Guid.NewGuid(), BusinessId = business.Id, HolidayDate = Christmas }));
    }

    [Fact]
    public async Task Delete_RemovesHoliday()
    {
        var business = await SeedBusinessAsync();
        var repo = new BusinessHolidayRepository(_db);
        var holiday = new BusinessHoliday { Id = Guid.NewGuid(), BusinessId = business.Id, HolidayDate = Christmas };
        await repo.AddAsync(holiday);

        await repo.DeleteAsync(holiday.Id);

        Assert.Empty(await repo.ListByBusinessAsync(business.Id));
    }

    private async Task<Business> SeedBusinessAsync()
    {
        var owner = new User { Id = Guid.NewGuid(), Email = $"o-{Guid.NewGuid():N}@t.local", PasswordHash = "h", Name = "O", Type = "owner" };
        var free = await _db.PricingTiers.AsNoTracking().SingleAsync(t => t.Code == "free");
        var business = new Business { Id = Guid.NewGuid(), OwnerId = owner.Id, TierId = free.Id, Name = "Biz" };
        _db.Users.Add(owner);
        _db.Businesses.Add(business);
        await _db.SaveChangesAsync();
        return business;
    }
}
