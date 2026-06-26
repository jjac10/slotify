using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Infrastructure.Data;
using Slotify.Infrastructure.Repositories;

namespace Slotify.Tests.Integration;

/// <summary>
/// Capa de datos de `business_hours`: horario semanal del negocio, "reemplazar
/// todo", UNIQUE por día y CHECK apertura&lt;cierre. Schema: DATA_MODEL.md.
/// </summary>
public class BusinessHourRepositoryTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private SlotifyDbContext _db = null!;

    public BusinessHourRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _db = _fixture.CreateContext();
        await _db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task ReplaceForBusiness_PersistsHours_AndListReturnsThem()
    {
        var business = await SeedBusinessAsync();
        var repo = new BusinessHourRepository(_db);

        await repo.ReplaceForBusinessAsync(business.Id, new[]
        {
            Hour(business.Id, 1, new TimeOnly(9, 0), new TimeOnly(18, 0)),
            Hour(business.Id, 2, new TimeOnly(9, 0), new TimeOnly(18, 0)),
            Closed(business.Id, 0),
        });

        var list = await repo.ListByBusinessAsync(business.Id);
        Assert.Equal(3, list.Count);
        Assert.Contains(list, h => h.DayOfWeek == 1 && h.OpeningTime == new TimeOnly(9, 0));
        Assert.Contains(list, h => h.DayOfWeek == 0 && h.IsClosed);
    }

    [Fact]
    public async Task ReplaceForBusiness_Twice_OverwritesPrevious()
    {
        var business = await SeedBusinessAsync();
        var repo = new BusinessHourRepository(_db);
        await repo.ReplaceForBusinessAsync(business.Id, new[] { Hour(business.Id, 1, new TimeOnly(9, 0), new TimeOnly(18, 0)) });

        await repo.ReplaceForBusinessAsync(business.Id, new[] { Hour(business.Id, 1, new TimeOnly(10, 0), new TimeOnly(14, 0)) });

        var list = await repo.ListByBusinessAsync(business.Id);
        Assert.Single(list);
        Assert.Equal(new TimeOnly(10, 0), list[0].OpeningTime);
    }

    [Fact]
    public async Task ReplaceForBusiness_DuplicateDay_IsRejected()
    {
        var business = await SeedBusinessAsync();
        var repo = new BusinessHourRepository(_db);

        await Assert.ThrowsAsync<DbUpdateException>(() => repo.ReplaceForBusinessAsync(business.Id, new[]
        {
            Hour(business.Id, 1, new TimeOnly(9, 0), new TimeOnly(13, 0)),
            Hour(business.Id, 1, new TimeOnly(14, 0), new TimeOnly(18, 0)),
        }));
    }

    [Fact]
    public async Task ReplaceForBusiness_OpeningAfterClosing_IsRejectedByCheck()
    {
        var business = await SeedBusinessAsync();
        var repo = new BusinessHourRepository(_db);

        await Assert.ThrowsAsync<DbUpdateException>(() => repo.ReplaceForBusinessAsync(business.Id, new[]
        {
            Hour(business.Id, 1, new TimeOnly(18, 0), new TimeOnly(9, 0)),
        }));
    }

    private static BusinessHour Hour(Guid businessId, int day, TimeOnly open, TimeOnly close) => new()
    {
        Id = Guid.NewGuid(),
        BusinessId = businessId,
        DayOfWeek = day,
        IsClosed = false,
        OpeningTime = open,
        ClosingTime = close,
    };

    private static BusinessHour Closed(Guid businessId, int day) => new()
    {
        Id = Guid.NewGuid(),
        BusinessId = businessId,
        DayOfWeek = day,
        IsClosed = true,
    };

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
