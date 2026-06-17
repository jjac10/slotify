using Slotify.Domain.Entities;
using Slotify.Infrastructure.Data;
using Slotify.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Slotify.Tests.Integration;

/// <summary>
/// Agregados de `reservations` para el panel del owner: contadores con ventana
/// temporal, ingresos del mes (precio del servicio de cada reserva) y próximas
/// reservas ordenadas y limitadas.
/// </summary>
public class DashboardRepositoryTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private SlotifyDbContext _db = null!;

    public DashboardRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _db = _fixture.CreateContext();
        await _db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    private static readonly DateTime June = new(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime JuneStart = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime JulyStart = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CountByBusinessAsync_NoWindow_CountsAllNonCancelled()
    {
        var ctx = await SeedAsync();
        var repo = new ReservationRepository(_db);
        await repo.AddAsync(NewReservation(ctx, June, June.AddMinutes(30)));
        await repo.AddAsync(NewReservation(ctx, June.AddHours(2), June.AddHours(2).AddMinutes(30)));

        Assert.Equal(2, await repo.CountByBusinessAsync(ctx.businessId, null, null));
    }

    [Fact]
    public async Task CountByBusinessAsync_WithWindow_CountsOnlyInsideRange()
    {
        var ctx = await SeedAsync();
        var repo = new ReservationRepository(_db);
        await repo.AddAsync(NewReservation(ctx, June, June.AddMinutes(30)));                       // junio
        await repo.AddAsync(NewReservation(ctx, June.AddMonths(1), June.AddMonths(1).AddMinutes(30))); // julio (fuera)

        Assert.Equal(1, await repo.CountByBusinessAsync(ctx.businessId, JuneStart, JulyStart));
    }

    [Fact]
    public async Task SumRevenueByBusinessAsync_SumsServicePriceOfReservationsInWindow()
    {
        var ctx = await SeedAsync(price: 25m);
        var repo = new ReservationRepository(_db);
        await repo.AddAsync(NewReservation(ctx, June, June.AddMinutes(30)));
        await repo.AddAsync(NewReservation(ctx, June.AddHours(2), June.AddHours(2).AddMinutes(30)));
        await repo.AddAsync(NewReservation(ctx, June.AddMonths(1), June.AddMonths(1).AddMinutes(30))); // julio (fuera)

        Assert.Equal(50m, await repo.SumRevenueByBusinessAsync(ctx.businessId, JuneStart, JulyStart));
    }

    [Fact]
    public async Task SumRevenueByBusinessAsync_FreeService_CountsAsZero()
    {
        var ctx = await SeedAsync(price: null); // servicio gratuito
        var repo = new ReservationRepository(_db);
        await repo.AddAsync(NewReservation(ctx, June, June.AddMinutes(30)));

        Assert.Equal(0m, await repo.SumRevenueByBusinessAsync(ctx.businessId, JuneStart, JulyStart));
    }

    [Fact]
    public async Task ListUpcomingByBusinessAsync_ReturnsFutureOrderedAndLimited()
    {
        var ctx = await SeedAsync();
        var repo = new ReservationRepository(_db);
        var now = June;
        await repo.AddAsync(NewReservation(ctx, now.AddDays(-1), now.AddDays(-1).AddMinutes(30))); // pasada (fuera)
        await repo.AddAsync(NewReservation(ctx, now.AddHours(3), now.AddHours(3).AddMinutes(30)));
        await repo.AddAsync(NewReservation(ctx, now.AddHours(1), now.AddHours(1).AddMinutes(30)));

        var upcoming = await repo.ListUpcomingByBusinessAsync(ctx.businessId, now, limit: 5);

        Assert.Equal(2, upcoming.Count);
        Assert.True(upcoming[0].StartTime < upcoming[1].StartTime); // ordenadas por inicio ascendente
    }

    [Fact]
    public async Task ListUpcomingByBusinessAsync_RespectsLimit()
    {
        var ctx = await SeedAsync();
        var repo = new ReservationRepository(_db);
        for (var i = 1; i <= 4; i++)
            await repo.AddAsync(NewReservation(ctx, June.AddHours(i), June.AddHours(i).AddMinutes(30)));

        var upcoming = await repo.ListUpcomingByBusinessAsync(ctx.businessId, June, limit: 2);

        Assert.Equal(2, upcoming.Count);
    }

    private static Reservation NewReservation((Guid businessId, Guid serviceId, Guid staffId, Guid guestId) ctx,
        DateTime start, DateTime end) => new()
    {
        Id = Guid.NewGuid(),
        BusinessId = ctx.businessId,
        ServiceId = ctx.serviceId,
        StaffId = ctx.staffId,
        GuestId = ctx.guestId,
        StartTime = start,
        EndTime = end,
        Status = "pending",
    };

    private async Task<(Guid businessId, Guid serviceId, Guid staffId, Guid guestId)> SeedAsync(decimal? price = 25m)
    {
        var owner = new User { Id = Guid.NewGuid(), Email = $"o-{Guid.NewGuid():N}@t.local", PasswordHash = "h", Name = "O", Type = "owner" };
        var free = await _db.PricingTiers.AsNoTracking().SingleAsync(t => t.Code == "free");
        var business = new Business { Id = Guid.NewGuid(), OwnerId = owner.Id, TierId = free.Id, Name = "Biz" };
        var staff = new Staff { Id = Guid.NewGuid(), BusinessId = business.Id, UserId = owner.Id, Role = "owner", Name = "O" };
        var service = new Service { Id = Guid.NewGuid(), BusinessId = business.Id, Name = "Corte", DurationMinutes = 30, Price = price };
        var guest = new Guest { Id = Guid.NewGuid(), BusinessId = business.Id, Name = "Juan", PhoneHash = $"ph-{Guid.NewGuid():N}" };
        _db.Users.Add(owner);
        _db.Businesses.Add(business);
        _db.Staff.Add(staff);
        _db.Services.Add(service);
        _db.Guests.Add(guest);
        await _db.SaveChangesAsync();
        return (business.Id, service.Id, staff.Id, guest.Id);
    }
}
