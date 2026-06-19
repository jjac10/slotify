using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Services;
using Slotify.Infrastructure.Data;
using Slotify.Infrastructure.Repositories;
using Slotify.Infrastructure.Security;

namespace Slotify.Tests.Integration;

/// <summary>
/// Flujo de reserva end-to-end contra Postgres: alta de invitado (cifrado real),
/// persistencia y anti-doble-booking.
/// </summary>
public class BookingFlowIntegrationTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private SlotifyDbContext _db = null!;

    public BookingFlowIntegrationTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _db = _fixture.CreateContext();
        await _db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    private static readonly DateTime At10 = new(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc);

    private static readonly CryptoOptions Crypto = new()
    {
        EncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        BlindIndexKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
    };

    private BookingService NewBookingService(SlotifyDbContext db) => new(
        new ReservationRepository(db), new ServiceRepository(db), new StaffRepository(db),
        new GuestRepository(db), new AesGcmCryptoService(Crypto), new HmacBlindIndex(Crypto),
        new FreemiumLimitService(new TierRepository(db), new StaffRepository(db), new ServiceRepository(db), new ReservationRepository(db)),
        new BusinessRepository(db));

    [Fact]
    public async Task CreateAsync_GuestBooking_PersistsReservationAndGuest()
    {
        var ctx = await SeedAsync();
        var request = new CreateReservationRequest(ctx.businessId, ctx.serviceId, ctx.staffId, At10, "Juan", "+34912345678", null);

        var result = await NewBookingService(_db).CreateAsync(request, userId: null);

        await using var verify = _fixture.CreateContext();
        var reservation = await verify.Reservations.AsNoTracking().SingleAsync(r => r.Id == result.Id);
        Assert.NotNull(reservation.GuestId);
        Assert.Equal(At10.AddMinutes(30), reservation.EndTime); // duración del servicio = 30
        var guest = await verify.Guests.AsNoTracking().SingleAsync(g => g.Id == reservation.GuestId);
        Assert.NotNull(guest.PhoneEncrypted);
        Assert.NotEqual("+34912345678", guest.PhoneEncrypted); // cifrado, no en claro
    }

    [Fact]
    public async Task CreateAsync_OverlappingBooking_ThrowsSlotUnavailable()
    {
        var ctx = await SeedAsync();
        var first = new CreateReservationRequest(ctx.businessId, ctx.serviceId, ctx.staffId, At10, "Juan", "+34900000001", null);
        await NewBookingService(_db).CreateAsync(first, userId: null);

        // Segundo invitado, mismo staff, solapando.
        var second = new CreateReservationRequest(ctx.businessId, ctx.serviceId, ctx.staffId, At10.AddMinutes(15), "Ana", "+34900000002", null);

        await Assert.ThrowsAsync<SlotUnavailableException>(
            () => NewBookingService(_db).CreateAsync(second, userId: null));
    }

    private async Task<(Guid businessId, Guid serviceId, Guid staffId)> SeedAsync()
    {
        var owner = new User { Id = Guid.NewGuid(), Email = $"o-{Guid.NewGuid():N}@t.local", PasswordHash = "h", Name = "O", Type = "owner" };
        var free = await _db.PricingTiers.AsNoTracking().SingleAsync(t => t.Code == "free");
        var business = new Business { Id = Guid.NewGuid(), OwnerId = owner.Id, TierId = free.Id, Name = "Biz" };
        var staff = new Staff { Id = Guid.NewGuid(), BusinessId = business.Id, UserId = owner.Id, Role = "owner", Name = "O" };
        var service = new Service { Id = Guid.NewGuid(), BusinessId = business.Id, Name = "Corte", DurationMinutes = 30 };
        _db.Users.Add(owner);
        _db.Businesses.Add(business);
        _db.Staff.Add(staff);
        _db.Services.Add(service);
        await _db.SaveChangesAsync();
        return (business.Id, service.Id, staff.Id);
    }
}
