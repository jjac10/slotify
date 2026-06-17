using Moq;
using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Lógica de creación de reservas (guest o usuario logueado), cálculo de endTime,
/// dedupe/cifrado de invitado y validación de solapamiento.
/// </summary>
public class BookingServiceTests
{
    private readonly Mock<IReservationRepository> _reservations = new();
    private readonly Mock<IServiceRepository> _services = new();
    private readonly Mock<IStaffRepository> _staff = new();
    private readonly Mock<IGuestRepository> _guests = new();
    private readonly Mock<ICryptoService> _crypto = new();
    private readonly Mock<IBlindIndex> _blindIndex = new();
    private readonly Mock<IFreemiumLimitService> _limits = new();

    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _serviceId = Guid.NewGuid();
    private readonly Guid _staffId = Guid.NewGuid();
    private static readonly DateTime Start = new(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc);

    public BookingServiceTests()
    {
        // Por defecto el negocio está dentro del límite Freemium; los tests que
        // prueban el bloqueo lo sobrescriben explícitamente.
        _limits.Setup(l => l.CanAddReservationThisMonthAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private BookingService CreateService() =>
        new(_reservations.Object, _services.Object, _staff.Object, _guests.Object, _crypto.Object, _blindIndex.Object, _limits.Object);

    private void SetupValidServiceAndStaff(int duration = 30)
    {
        _services.Setup(s => s.GetByIdAsync(_serviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Service { Id = _serviceId, BusinessId = _businessId, Name = "Corte", DurationMinutes = duration });
        _staff.Setup(s => s.GetByIdAsync(_staffId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Staff { Id = _staffId, BusinessId = _businessId, Role = "owner", Name = "O" });
        _reservations.Setup(r => r.HasOverlapAsync(_staffId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _reservations.Setup(r => r.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private CreateReservationRequest GuestRequest(string? phone = "+34912345678", string? email = null) =>
        new(_businessId, _serviceId, _staffId, Start, "Juan", phone, email);

    [Fact]
    public async Task CreateAsync_GuestWithPhone_CreatesGuestAndReservation_WithComputedEndTime()
    {
        SetupValidServiceAndStaff(duration: 45);
        _guests.Setup(g => g.FindByHashAsync(_businessId, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guest?)null);
        _crypto.Setup(c => c.Encrypt(It.IsAny<string>())).Returns("ENC");
        _blindIndex.Setup(b => b.Compute(It.IsAny<string>())).Returns("HASH");
        Guest? savedGuest = null;
        Reservation? savedReservation = null;
        _guests.Setup(g => g.AddAsync(It.IsAny<Guest>(), It.IsAny<CancellationToken>()))
            .Callback<Guest, CancellationToken>((g, _) => savedGuest = g).Returns(Task.CompletedTask);
        _reservations.Setup(r => r.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()))
            .Callback<Reservation, CancellationToken>((r, _) => savedReservation = r).Returns(Task.CompletedTask);

        var result = await CreateService().CreateAsync(GuestRequest(), userId: null);

        Assert.NotNull(savedGuest);
        Assert.Equal(_businessId, savedGuest!.BusinessId);
        Assert.Equal("ENC", savedGuest.PhoneEncrypted);
        Assert.Equal("HASH", savedGuest.PhoneHash);

        Assert.NotNull(savedReservation);
        Assert.Equal(savedGuest.Id, savedReservation!.GuestId);
        Assert.Null(savedReservation.UserId);
        Assert.Equal(Start, savedReservation.StartTime);
        Assert.Equal(Start.AddMinutes(45), savedReservation.EndTime); // endTime = start + duración
        Assert.Equal(savedReservation.Id, result.Id);
    }

    [Fact]
    public async Task CreateAsync_LoggedUser_UsesUserId_AndSkipsGuest()
    {
        SetupValidServiceAndStaff();
        var userId = Guid.NewGuid();
        Reservation? saved = null;
        _reservations.Setup(r => r.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()))
            .Callback<Reservation, CancellationToken>((r, _) => saved = r).Returns(Task.CompletedTask);

        var request = new CreateReservationRequest(_businessId, _serviceId, _staffId, Start, null, null, null);
        await CreateService().CreateAsync(request, userId);

        Assert.Equal(userId, saved!.UserId);
        Assert.Null(saved.GuestId);
        _guests.Verify(g => g.AddAsync(It.IsAny<Guest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_GuestAlreadyExists_ReusesGuest()
    {
        SetupValidServiceAndStaff();
        var existing = new Guest { Id = Guid.NewGuid(), BusinessId = _businessId, Name = "Juan", PhoneHash = "HASH" };
        _blindIndex.Setup(b => b.Compute(It.IsAny<string>())).Returns("HASH");
        _guests.Setup(g => g.FindByHashAsync(_businessId, "HASH", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        Reservation? saved = null;
        _reservations.Setup(r => r.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()))
            .Callback<Reservation, CancellationToken>((r, _) => saved = r).Returns(Task.CompletedTask);

        await CreateService().CreateAsync(GuestRequest(), userId: null);

        Assert.Equal(existing.Id, saved!.GuestId);
        _guests.Verify(g => g.AddAsync(It.IsAny<Guest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenSlotOverlaps_Throws_AndDoesNotPersist()
    {
        SetupValidServiceAndStaff();
        _reservations.Setup(r => r.HasOverlapAsync(_staffId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<SlotUnavailableException>(
            () => CreateService().CreateAsync(GuestRequest(), userId: null));
        _reservations.Verify(r => r.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null, null)]                       // ni teléfono ni email
    [InlineData("+34912345678", "a@b.com")]        // ambos
    public async Task CreateAsync_GuestWithInvalidContact_Throws(string? phone, string? email)
    {
        SetupValidServiceAndStaff();

        await Assert.ThrowsAsync<InvalidGuestContactException>(
            () => CreateService().CreateAsync(GuestRequest(phone, email), userId: null));
    }

    [Fact]
    public async Task CreateAsync_WhenServiceNotInBusiness_Throws()
    {
        _services.Setup(s => s.GetByIdAsync(_serviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Service?)null);

        await Assert.ThrowsAsync<ServiceNotFoundException>(
            () => CreateService().CreateAsync(GuestRequest(), userId: null));
    }

    [Fact]
    public async Task CreateAsync_WhenMonthlyFreemiumLimitReached_Throws_AndDoesNotPersist()
    {
        SetupValidServiceAndStaff();
        _limits.Setup(l => l.CanAddReservationThisMonthAsync(_businessId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<FreemiumLimitReachedException>(
            () => CreateService().CreateAsync(GuestRequest(), userId: null));
        _reservations.Verify(r => r.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenUnderMonthlyLimit_Persists()
    {
        SetupValidServiceAndStaff();
        _guests.Setup(g => g.FindByHashAsync(_businessId, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guest?)null);
        _crypto.Setup(c => c.Encrypt(It.IsAny<string>())).Returns("ENC");
        _blindIndex.Setup(b => b.Compute(It.IsAny<string>())).Returns("HASH");

        await CreateService().CreateAsync(GuestRequest(), userId: null);

        _reservations.Verify(r => r.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Once);
        _limits.Verify(l => l.CanAddReservationThisMonthAsync(_businessId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
