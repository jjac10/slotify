using Moq;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Despacho de avisos: respeta los canales configurados (email/WhatsApp), resuelve el
/// destinatario (usuario registrado o invitado descifrado) y, en recordatorios, solo
/// envía dentro de la ventana de antelación del negocio y una sola vez.
/// </summary>
public class NotificationServiceTests
{
    private readonly Mock<IBusinessRepository> _businesses = new();
    private readonly Mock<IReservationRepository> _reservations = new();
    private readonly Mock<IAuthRepository> _users = new();
    private readonly Mock<IGuestRepository> _guests = new();
    private readonly Mock<INotificationRepository> _notifications = new();
    private readonly Mock<INotificationSender> _sender = new();
    private readonly Mock<ICryptoService> _crypto = new();

    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _reservationId = Guid.NewGuid();
    private static readonly DateTime Start = DateTime.UtcNow.AddHours(12);

    private NotificationService CreateService() =>
        new(_businesses.Object, _reservations.Object, _users.Object, _guests.Object,
            _notifications.Object, _sender.Object, _crypto.Object);

    private Business Business(bool email = true, bool whatsapp = false, int reminderHours = 24) =>
        new() { Id = _businessId, OwnerId = Guid.NewGuid(), TierId = Guid.NewGuid(), Name = "Barberia",
            NotifyByEmail = email, NotifyByWhatsapp = whatsapp, ReminderHoursBefore = reminderHours };

    [Fact]
    public async Task DispatchEvent_EmailEnabled_UserReservation_RecordsEmail()
    {
        var userId = Guid.NewGuid();
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(Business(email: true, whatsapp: false));
        _users.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, Email = "ana@x.com", Name = "Ana", Phone = "+34600", PasswordHash = "x" });
        var saved = new List<Notification>();
        _notifications.Setup(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => saved.Add(n)).Returns(Task.CompletedTask);

        await CreateService().DispatchEventAsync(new NotificationContext(_businessId, _reservationId, userId, null, Start), "created");

        var n = Assert.Single(saved);
        Assert.Equal("email", n.Channel);
        Assert.Equal("ana@x.com", n.Recipient);
        Assert.Equal("created", n.EventType);
        _sender.Verify(s => s.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchEvent_BothChannels_GuestReservation_RecordsEmailAndWhatsapp()
    {
        var guestId = Guid.NewGuid();
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(Business(email: true, whatsapp: true));
        _guests.Setup(g => g.GetByIdAsync(guestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Guest { Id = guestId, BusinessId = _businessId, Name = "Juan", EmailEncrypted = "E", PhoneEncrypted = "P" });
        _crypto.Setup(c => c.Decrypt("E")).Returns("juan@x.com");
        _crypto.Setup(c => c.Decrypt("P")).Returns("+34611222333");
        var saved = new List<Notification>();
        _notifications.Setup(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => saved.Add(n)).Returns(Task.CompletedTask);

        await CreateService().DispatchEventAsync(new NotificationContext(_businessId, _reservationId, null, guestId, Start), "cancelled");

        Assert.Equal(2, saved.Count);
        Assert.Contains(saved, n => n.Channel == "email" && n.Recipient == "juan@x.com");
        Assert.Contains(saved, n => n.Channel == "whatsapp" && n.Recipient == "+34611222333");
    }

    [Fact]
    public async Task DispatchEvent_NoChannels_DoesNothing()
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(Business(email: false, whatsapp: false));

        await CreateService().DispatchEventAsync(new NotificationContext(_businessId, _reservationId, Guid.NewGuid(), null, Start), "created");

        _notifications.Verify(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DispatchEvent_WhatsappEnabled_NoPhone_Skips()
    {
        var userId = Guid.NewGuid();
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(Business(email: false, whatsapp: true));
        _users.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, Email = "ana@x.com", Name = "Ana", Phone = null, PasswordHash = "x" });

        await CreateService().DispatchEventAsync(new NotificationContext(_businessId, _reservationId, userId, null, Start), "created");

        _notifications.Verify(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Recordatorios ---

    private void SetupReminderCandidate(DateTime start, Business business, Guid userId)
    {
        var reservation = new Reservation
        {
            Id = _reservationId, BusinessId = _businessId, ServiceId = Guid.NewGuid(), StaffId = Guid.NewGuid(),
            UserId = userId, Status = "confirmed", StartTime = start, EndTime = start.AddMinutes(30), Business = business,
        };
        _reservations.Setup(r => r.ListUpcomingForReminderAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { reservation });
        _users.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, Email = "ana@x.com", Name = "Ana", PasswordHash = "x" });
    }

    [Fact]
    public async Task DispatchDueReminders_WithinWindow_NotAlreadyReminded_Sends()
    {
        var now = DateTime.UtcNow;
        SetupReminderCandidate(now.AddHours(12), Business(email: true, reminderHours: 24), Guid.NewGuid());
        _notifications.Setup(n => n.ExistsForReservationAsync(_reservationId, "reminder", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        Notification? saved = null;
        _notifications.Setup(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => saved = n).Returns(Task.CompletedTask);

        var count = await CreateService().DispatchDueRemindersAsync(now);

        Assert.Equal(1, count);
        Assert.Equal("reminder", saved!.EventType);
    }

    [Fact]
    public async Task DispatchDueReminders_OutsideWindow_Skips()
    {
        var now = DateTime.UtcNow;
        SetupReminderCandidate(now.AddHours(48), Business(email: true, reminderHours: 24), Guid.NewGuid()); // aún a 48h, ventana 24h
        _notifications.Setup(n => n.ExistsForReservationAsync(_reservationId, "reminder", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var count = await CreateService().DispatchDueRemindersAsync(now);

        Assert.Equal(0, count);
        _notifications.Verify(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DispatchDueReminders_AlreadyReminded_Skips()
    {
        var now = DateTime.UtcNow;
        SetupReminderCandidate(now.AddHours(12), Business(email: true, reminderHours: 24), Guid.NewGuid());
        _notifications.Setup(n => n.ExistsForReservationAsync(_reservationId, "reminder", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var count = await CreateService().DispatchDueRemindersAsync(now);

        Assert.Equal(0, count);
        _notifications.Verify(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
