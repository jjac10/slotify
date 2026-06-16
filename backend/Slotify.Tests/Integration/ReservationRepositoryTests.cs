using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Infrastructure.Data;
using Slotify.Infrastructure.Repositories;

namespace Slotify.Tests.Integration;

/// <summary>
/// Capa de datos de `reservations`: anti-doble-booking robusto vía exclusion
/// constraint (btree_gist) y CHECK user_or_guest. Schema: DATA_MODEL.md / ADR #4.
/// </summary>
public class ReservationRepositoryTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private SlotifyDbContext _db = null!;

    public ReservationRepositoryTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _db = _fixture.CreateContext();
        await _db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    private static readonly DateTime At10 = new(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AddAsync_ValidGuestReservation_Persists()
    {
        var ctx = await SeedAsync();
        var repo = new ReservationRepository(_db);

        var reservation = NewReservation(ctx, At10, At10.AddMinutes(30));
        await repo.AddAsync(reservation);

        await using var verify = _fixture.CreateContext();
        Assert.True(await verify.Reservations.AnyAsync(r => r.Id == reservation.Id));
    }

    [Fact]
    public async Task AddAsync_OverlappingSameStaff_IsRejectedByExclusionConstraint()
    {
        var ctx = await SeedAsync();
        var repo = new ReservationRepository(_db);
        await repo.AddAsync(NewReservation(ctx, At10, At10.AddMinutes(30)));        // 10:00–10:30

        // 10:15–10:45 solapa con la anterior para el mismo staff → el constraint la rechaza,
        // y el repo la traduce a SlotUnavailableException (409).
        await Assert.ThrowsAsync<SlotUnavailableException>(() =>
            repo.AddAsync(NewReservation(ctx, At10.AddMinutes(15), At10.AddMinutes(45))));
    }

    [Fact]
    public async Task AddAsync_BackToBackSameStaff_BothPersist()
    {
        var ctx = await SeedAsync();
        var repo = new ReservationRepository(_db);

        await repo.AddAsync(NewReservation(ctx, At10, At10.AddMinutes(30)));         // 10:00–10:30
        await repo.AddAsync(NewReservation(ctx, At10.AddMinutes(30), At10.AddMinutes(60))); // 10:30–11:00

        await using var verify = _fixture.CreateContext();
        Assert.Equal(2, await verify.Reservations.CountAsync(r => r.StaffId == ctx.staffId));
    }

    [Fact]
    public async Task AddAsync_BothUserAndGuestNull_IsRejectedByCheck()
    {
        var ctx = await SeedAsync();
        var repo = new ReservationRepository(_db);

        var r = NewReservation(ctx, At10, At10.AddMinutes(30));
        r.GuestId = null; // ni user ni guest → viola CHECK user_or_guest

        await Assert.ThrowsAsync<DbUpdateException>(() => repo.AddAsync(r));
    }

    [Fact]
    public async Task HasOverlapAsync_ReturnsTrue_WhenRangeOverlaps()
    {
        var ctx = await SeedAsync();
        var repo = new ReservationRepository(_db);
        await repo.AddAsync(NewReservation(ctx, At10, At10.AddMinutes(30)));

        Assert.True(await repo.HasOverlapAsync(ctx.staffId, At10.AddMinutes(15), At10.AddMinutes(45)));
        Assert.False(await repo.HasOverlapAsync(ctx.staffId, At10.AddMinutes(30), At10.AddMinutes(60)));
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

    private async Task<(Guid businessId, Guid serviceId, Guid staffId, Guid guestId)> SeedAsync()
    {
        var owner = new User { Id = Guid.NewGuid(), Email = $"o-{Guid.NewGuid():N}@t.local", PasswordHash = "h", Name = "O", Type = "owner" };
        var free = await _db.PricingTiers.AsNoTracking().SingleAsync(t => t.Code == "free");
        var business = new Business { Id = Guid.NewGuid(), OwnerId = owner.Id, TierId = free.Id, Name = "Biz" };
        var staff = new Staff { Id = Guid.NewGuid(), BusinessId = business.Id, UserId = owner.Id, Role = "owner", Name = "O" };
        var service = new Service { Id = Guid.NewGuid(), BusinessId = business.Id, Name = "Corte", DurationMinutes = 30 };
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
