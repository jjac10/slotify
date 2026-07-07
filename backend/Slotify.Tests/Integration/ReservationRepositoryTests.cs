using Microsoft.EntityFrameworkCore;
using Slotify.Domain.DTOs;
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

    [Fact]
    public async Task HasOverlapAsync_ExcludingSelf_IgnoresThatReservation()
    {
        var ctx = await SeedAsync();
        var repo = new ReservationRepository(_db);
        var r = NewReservation(ctx, At10, At10.AddMinutes(30));
        await repo.AddAsync(r);

        // El mismo rango solapa, pero al excluirse a sí misma no cuenta.
        Assert.False(await repo.HasOverlapAsync(ctx.staffId, At10, At10.AddMinutes(30), excludeReservationId: r.Id));
        Assert.True(await repo.HasOverlapAsync(ctx.staffId, At10, At10.AddMinutes(30), excludeReservationId: Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateAsync_MovesToFreeSlot_Persists()
    {
        var ctx = await SeedAsync();
        var repo = new ReservationRepository(_db);
        var r = NewReservation(ctx, At10, At10.AddMinutes(30));
        await repo.AddAsync(r);

        r.StartTime = At10.AddHours(1);
        r.EndTime = At10.AddHours(1).AddMinutes(30);
        r.Version++;
        await repo.UpdateAsync(r);

        await using var verify = _fixture.CreateContext();
        var saved = await verify.Reservations.AsNoTracking().FirstAsync(x => x.Id == r.Id);
        Assert.Equal(At10.AddHours(1), saved.StartTime);
        Assert.Equal(1, saved.Version);
    }

    [Fact]
    public async Task UpdateAsync_OntoOverlappingSameStaff_IsRejectedByExclusionConstraint()
    {
        var ctx = await SeedAsync();
        var repo = new ReservationRepository(_db);
        await repo.AddAsync(NewReservation(ctx, At10, At10.AddMinutes(30)));           // A: 10:00–10:30
        var b = NewReservation(ctx, At10.AddHours(1), At10.AddHours(1).AddMinutes(30)); // B: 11:00–11:30
        await repo.AddAsync(b);

        // Mover B sobre A → el exclusion constraint la rechaza (traducido a 409).
        b.StartTime = At10.AddMinutes(15);
        b.EndTime = At10.AddMinutes(45);
        b.Version++;
        await Assert.ThrowsAsync<SlotUnavailableException>(() => repo.UpdateAsync(b));
    }

    [Fact]
    public async Task UpdateAsync_WithStaleVersion_ThrowsConcurrency()
    {
        var ctx = await SeedAsync();
        var r = NewReservation(ctx, At10, At10.AddMinutes(30));
        await new ReservationRepository(_db).AddAsync(r);

        // Dos copias cargan la misma fila (version 0) en contextos distintos.
        await using var ctxA = _fixture.CreateContext();
        await using var ctxB = _fixture.CreateContext();
        var copyA = await ctxA.Reservations.FirstAsync(x => x.Id == r.Id);
        var copyB = await ctxB.Reservations.FirstAsync(x => x.Id == r.Id);

        // A actualiza primero (version 0 → 1).
        copyA.StartTime = At10.AddHours(1); copyA.EndTime = At10.AddHours(1).AddMinutes(30); copyA.Version++;
        await new ReservationRepository(ctxA).UpdateAsync(copyA);

        // B actualiza con version obsoleta → optimistic locking lo rechaza.
        copyB.StartTime = At10.AddHours(2); copyB.EndTime = At10.AddHours(2).AddMinutes(30); copyB.Version++;
        await Assert.ThrowsAsync<ReservationConcurrencyException>(
            () => new ReservationRepository(ctxB).UpdateAsync(copyB));
    }

    [Fact]
    public async Task ListByBusinessAsync_FiltersByDateAndStaff_WithTotal()
    {
        var ctx = await SeedAsync();
        var repo = new ReservationRepository(_db);
        await repo.AddAsync(NewReservation(ctx, At10, At10.AddMinutes(30)));                       // 20-jun 10:00
        await repo.AddAsync(NewReservation(ctx, At10.AddDays(1), At10.AddDays(1).AddMinutes(30))); // 21-jun 10:00

        var (all, allTotal) = await repo.ListByBusinessAsync(ctx.businessId, date: null, staffId: null, skip: 0, take: 20);
        Assert.Equal(2, all.Count);
        Assert.Equal(2, allTotal);

        var (onlyDay, dayTotal) = await repo.ListByBusinessAsync(ctx.businessId, date: DateOnly.FromDateTime(At10), staffId: null, skip: 0, take: 20);
        Assert.Single(onlyDay);
        Assert.Equal(1, dayTotal);

        var (byStaff, _) = await repo.ListByBusinessAsync(ctx.businessId, date: null, staffId: ctx.staffId, skip: 0, take: 20);
        Assert.Equal(2, byStaff.Count);
        var (otherStaff, otherTotal) = await repo.ListByBusinessAsync(ctx.businessId, date: null, staffId: Guid.NewGuid(), skip: 0, take: 20);
        Assert.Empty(otherStaff);
        Assert.Equal(0, otherTotal);
    }

    [Fact]
    public async Task ListByBusinessAsync_PaginatesInDb_WithFilterTotal()
    {
        var ctx = await SeedAsync();
        var repo = new ReservationRepository(_db);
        for (var i = 0; i < 3; i++)
            await repo.AddAsync(NewReservation(ctx, At10.AddHours(i), At10.AddHours(i).AddMinutes(30)));

        var (page1, total1) = await repo.ListByBusinessAsync(ctx.businessId, null, null, skip: 0, take: 2);
        var (page2, total2) = await repo.ListByBusinessAsync(ctx.businessId, null, null, skip: 2, take: 2);

        // Total del filtro en TODAS las páginas; sin solapes y orden por inicio.
        Assert.Equal(3, total1);
        Assert.Equal(3, total2);
        Assert.Equal(2, page1.Count);
        Assert.Single(page2);
        var starts = page1.Concat(page2).Select(r => r.StartTime).ToList();
        Assert.Equal(3, page1.Concat(page2).Select(r => r.Id).Distinct().Count());
        Assert.Equal(starts.OrderBy(s => s), starts);
    }

    [Fact]
    public async Task ListByUserAsync_ReturnsUserAndLinkedGuestReservations_InOneQuery()
    {
        var ctx = await SeedAsync();
        var repo = new ReservationRepository(_db);
        // Una reserva de usuario, otra de SU invitado vinculado y otra de un invitado ajeno.
        var user = new User { Id = Guid.NewGuid(), Email = $"u-{Guid.NewGuid():N}@t.local", PasswordHash = "h", Name = "U", Type = "customer" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        var mine = NewReservation(ctx, At10, At10.AddMinutes(30));
        mine.GuestId = null; mine.UserId = user.Id;
        await repo.AddAsync(mine);
        var viaGuest = NewReservation(ctx, At10.AddHours(1), At10.AddHours(1).AddMinutes(30)); // invitado vinculado
        await repo.AddAsync(viaGuest);
        // Sin guestIds vinculados → solo la directa.
        var (onlyDirect, directTotal) = await repo.ListByUserAsync(
            user.Id, [], ReservationScope.All, DateTime.UtcNow, skip: 0, take: 20);
        Assert.Single(onlyDirect);
        Assert.Equal(1, directTotal);
        Assert.Equal(mine.Id, onlyDirect[0].Id);

        // Con el guest vinculado → ambas, ordenadas por inicio, con total coherente.
        var (both, bothTotal) = await repo.ListByUserAsync(
            user.Id, [ctx.guestId], ReservationScope.All, DateTime.UtcNow, skip: 0, take: 20);
        Assert.Equal(2, both.Count);
        Assert.Equal(2, bothTotal);
        Assert.Equal(new[] { mine.Id, viaGuest.Id }, both.Select(r => r.Id).ToList());
    }

    [Fact]
    public async Task ListByUserAsync_ScopeFiltersByStart_PastDescending_UpcomingAscending()
    {
        var ctx = await SeedAsync();
        var repo = new ReservationRepository(_db);
        var user = new User { Id = Guid.NewGuid(), Email = $"u-{Guid.NewGuid():N}@t.local", PasswordHash = "h", Name = "U", Type = "customer" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        // "now" fijo entre las dos pasadas y las dos futuras → test determinista.
        var now = At10.AddDays(10);
        var times = new[] { At10, At10.AddDays(1), now.AddDays(1), now.AddDays(2) };
        var ids = new List<Guid>();
        foreach (var t in times)
        {
            var r = NewReservation(ctx, t, t.AddMinutes(30));
            r.GuestId = null; r.UserId = user.Id;
            await repo.AddAsync(r);
            ids.Add(r.Id);
        }

        var (upcoming, upTotal) = await repo.ListByUserAsync(user.Id, [], ReservationScope.Upcoming, now, 0, 20);
        Assert.Equal(2, upTotal);
        Assert.Equal(new[] { ids[2], ids[3] }, upcoming.Select(r => r.Id).ToList()); // ascendente

        var (past, pastTotal) = await repo.ListByUserAsync(user.Id, [], ReservationScope.Past, now, 0, 20);
        Assert.Equal(2, pastTotal);
        Assert.Equal(new[] { ids[1], ids[0] }, past.Select(r => r.Id).ToList()); // descendente (reciente primero)

        var (all, allTotal) = await repo.ListByUserAsync(user.Id, [], ReservationScope.All, now, 0, 20);
        Assert.Equal(4, allTotal);
        Assert.Equal(ids, all.Select(r => r.Id).ToList()); // ascendente
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
