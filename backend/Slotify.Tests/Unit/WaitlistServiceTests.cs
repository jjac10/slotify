using Moq;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Lista de espera: un usuario registrado se apunta a un (servicio, día) SOLO si el día
/// está completo (si quedan huecos → 409, se reserva directamente), sin duplicarse y con
/// posición incremental. Al liberarse un hueco (cancelación), se avisa al primero de la
/// cola (waiting → notified) reutilizando el despacho de notificaciones; el aviso es
/// best-effort. Salir de la cola: solo el dueño de la entrada.
/// </summary>
public class WaitlistServiceTests
{
    private readonly Mock<IWaitlistRepository> _waitlists = new();
    private readonly Mock<IServiceRepository> _services = new();
    private readonly Mock<IDayAvailabilityChecker> _availability = new();
    private readonly Mock<IBusinessRepository> _businesses = new();
    private readonly Mock<INotificationRepository> _notifications = new();
    private readonly Mock<INotificationSender> _sender = new();
    private readonly Mock<IAuthRepository> _users = new();
    private readonly Mock<IGuestRepository> _guests = new();
    private readonly Mock<ICryptoService> _crypto = new();

    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _serviceId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private static readonly DateOnly Tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

    private NotificationService CreateNotifications() => new(
        _businesses.Object, Mock.Of<IReservationRepository>(), _users.Object, _guests.Object,
        _notifications.Object, _sender.Object, _crypto.Object);

    private WaitlistService CreateService() => new(
        _waitlists.Object, _services.Object, _availability.Object, _businesses.Object, CreateNotifications());

    private void SetupService()
        => _services.Setup(s => s.GetByIdAsync(_serviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Service { Id = _serviceId, BusinessId = _businessId, Name = "Corte", DurationMinutes = 30 });

    private void SetupDayFull(bool full = true)
        => _availability.Setup(a => a.HasFreeSlotAsync(_businessId, _serviceId, Tomorrow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(!full);

    // --- Apuntarse ------------------------------------------------------------

    [Fact]
    public async Task JoinAsync_DayFull_AddsEntryWithNextPosition()
    {
        SetupService();
        SetupDayFull();
        _waitlists.Setup(w => w.CountWaitingAsync(_serviceId, Tomorrow, It.IsAny<CancellationToken>())).ReturnsAsync(2);
        WaitlistEntry? added = null;
        _waitlists.Setup(w => w.AddAsync(It.IsAny<WaitlistEntry>(), It.IsAny<CancellationToken>()))
            .Callback<WaitlistEntry, CancellationToken>((e, _) => added = e).Returns(Task.CompletedTask);

        var result = await CreateService().JoinAsync(_businessId, _serviceId, Tomorrow, _userId);

        Assert.NotNull(added);
        Assert.Equal(3, added!.Position); // 2 esperando → entra el 3º
        Assert.Equal(_userId, added.UserId);
        Assert.Equal("waiting", added.Status);
        Assert.Equal(_businessId, added.BusinessId);
        Assert.Equal(3, result.Position);
    }

    [Fact]
    public async Task JoinAsync_DayStillHasFreeSlots_Throws_AndDoesNotAdd()
    {
        SetupService();
        SetupDayFull(full: false);

        await Assert.ThrowsAsync<WaitlistNotNeededException>(() =>
            CreateService().JoinAsync(_businessId, _serviceId, Tomorrow, _userId));

        _waitlists.Verify(w => w.AddAsync(It.IsAny<WaitlistEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task JoinAsync_AlreadyWaiting_Throws()
    {
        SetupService();
        SetupDayFull();
        _waitlists.Setup(w => w.ExistsForUserAsync(_serviceId, Tomorrow, _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<AlreadyOnWaitlistException>(() =>
            CreateService().JoinAsync(_businessId, _serviceId, Tomorrow, _userId));
    }

    [Fact]
    public async Task JoinAsync_PastDate_Throws()
    {
        SetupService();

        await Assert.ThrowsAsync<InvalidWaitlistDateException>(() => CreateService().JoinAsync(
            _businessId, _serviceId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), _userId));
    }

    [Fact]
    public async Task JoinAsync_ServiceOfAnotherBusiness_Throws()
    {
        _services.Setup(s => s.GetByIdAsync(_serviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Service { Id = _serviceId, BusinessId = Guid.NewGuid(), Name = "Corte", DurationMinutes = 30 });

        await Assert.ThrowsAsync<ServiceNotFoundException>(() =>
            CreateService().JoinAsync(_businessId, _serviceId, Tomorrow, _userId));
    }

    // --- Aviso al liberarse hueco ----------------------------------------------

    [Fact]
    public async Task NotifyNextAsync_MarksFirstWaitingAsNotified_AndDispatchesNotification()
    {
        var entry = new WaitlistEntry
        {
            Id = Guid.NewGuid(), BusinessId = _businessId, ServiceId = _serviceId,
            Date = Tomorrow, UserId = _userId, Position = 1, Status = "waiting",
        };
        _waitlists.Setup(w => w.FirstWaitingAsync(_serviceId, Tomorrow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        // Negocio con email activado y usuario con email → se despacha el aviso.
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = _businessId, OwnerId = Guid.NewGuid(), TierId = Guid.NewGuid(), Name = "Barbería", NotifyByEmail = true });
        _users.Setup(u => u.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = _userId, Email = "ana@example.com", Name = "Ana", PasswordHash = "x" });

        await CreateService().NotifyNextAsync(_serviceId, Tomorrow, Guid.NewGuid());

        Assert.Equal("notified", entry.Status);
        Assert.NotNull(entry.NotifiedAt);
        _waitlists.Verify(w => w.UpdateAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
        _sender.Verify(s => s.SendAsync(
            It.Is<Notification>(n => n.EventType == "waitlist_slot_freed" && n.Recipient == "ana@example.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifySlotFreedAsync_ConvertsUtcStartToLocalBusinessDay()
    {
        // 23:30 UTC del 1 de julio = 01:30 del 2 de julio en Europe/Madrid (verano):
        // la cola avisada es la del día LOCAL (2 de julio).
        var localDay = new DateOnly(2026, 7, 2);
        var entry = new WaitlistEntry
        {
            Id = Guid.NewGuid(), BusinessId = _businessId, ServiceId = _serviceId,
            Date = localDay, UserId = _userId, Position = 1, Status = "waiting",
        };
        _waitlists.Setup(w => w.FirstWaitingAsync(_serviceId, localDay, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = _businessId, OwnerId = Guid.NewGuid(), TierId = Guid.NewGuid(), Name = "Barbería", NotifyByEmail = true });
        _users.Setup(u => u.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = _userId, Email = "ana@example.com", Name = "Ana", PasswordHash = "x" });

        await CreateService().NotifySlotFreedAsync(
            _businessId, _serviceId, new DateTime(2026, 7, 1, 23, 30, 0, DateTimeKind.Utc), Guid.NewGuid());

        Assert.Equal("notified", entry.Status);
    }

    [Fact]
    public async Task NotifyNextAsync_EmptyQueue_DoesNothing()
    {
        await CreateService().NotifyNextAsync(_serviceId, Tomorrow, Guid.NewGuid());

        _waitlists.Verify(w => w.UpdateAsync(It.IsAny<WaitlistEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        _sender.Verify(s => s.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotifyNextAsync_SenderFails_StillMarksNotified_BestEffort()
    {
        var entry = new WaitlistEntry
        {
            Id = Guid.NewGuid(), BusinessId = _businessId, ServiceId = _serviceId,
            Date = Tomorrow, UserId = _userId, Position = 1, Status = "waiting",
        };
        _waitlists.Setup(w => w.FirstWaitingAsync(_serviceId, Tomorrow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = _businessId, OwnerId = Guid.NewGuid(), TierId = Guid.NewGuid(), Name = "Barbería", NotifyByEmail = true });
        _users.Setup(u => u.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = _userId, Email = "ana@example.com", Name = "Ana", PasswordHash = "x" });
        _sender.Setup(s => s.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("proveedor caído"));

        await CreateService().NotifyNextAsync(_serviceId, Tomorrow, Guid.NewGuid());

        Assert.Equal("notified", entry.Status); // el aviso es best-effort, la cola avanza
    }

    // --- Ver y salir de la cola --------------------------------------------------

    [Fact]
    public async Task ListMineAsync_MapsEntries()
    {
        _waitlists.Setup(w => w.ListByUserAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new WaitlistEntry
                {
                    Id = Guid.NewGuid(), BusinessId = _businessId, ServiceId = _serviceId, Date = Tomorrow,
                    UserId = _userId, Position = 2, Status = "waiting",
                    Business = new Business { Id = _businessId, OwnerId = Guid.NewGuid(), TierId = Guid.NewGuid(), Name = "Barbería" },
                    Service = new Service { Id = _serviceId, BusinessId = _businessId, Name = "Corte", DurationMinutes = 30 },
                },
            ]);

        var list = await CreateService().ListMineAsync(_userId);

        var dto = Assert.Single(list);
        Assert.Equal("Barbería", dto.BusinessName);
        Assert.Equal("Corte", dto.ServiceName);
        Assert.Equal(2, dto.Position);
    }

    [Fact]
    public async Task LeaveAsync_ByOwnerOfEntry_Deletes()
    {
        var entryId = Guid.NewGuid();
        _waitlists.Setup(w => w.GetByIdAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WaitlistEntry { Id = entryId, BusinessId = _businessId, ServiceId = _serviceId, Date = Tomorrow, UserId = _userId });

        await CreateService().LeaveAsync(entryId, _userId);

        _waitlists.Verify(w => w.DeleteAsync(entryId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LeaveAsync_ByAnotherUser_Throws_AndDoesNotDelete()
    {
        var entryId = Guid.NewGuid();
        _waitlists.Setup(w => w.GetByIdAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WaitlistEntry { Id = entryId, BusinessId = _businessId, ServiceId = _serviceId, Date = Tomorrow, UserId = _userId });

        await Assert.ThrowsAsync<WaitlistEntryNotFoundException>(() =>
            CreateService().LeaveAsync(entryId, Guid.NewGuid()));

        _waitlists.Verify(w => w.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LeaveAsync_UnknownEntry_Throws()
    {
        await Assert.ThrowsAsync<WaitlistEntryNotFoundException>(() =>
            CreateService().LeaveAsync(Guid.NewGuid(), _userId));
    }
}
