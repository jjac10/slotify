using Moq;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Cancelación de reservas: autorización (owner del negocio, staff o el propio
/// usuario de la reserva), auditoría antes de borrar y hard-delete (ADR #13/#14).
/// </summary>
public class ReservationManagementServiceTests
{
    private readonly Mock<IReservationRepository> _reservations = new();
    private readonly Mock<IBusinessRepository> _businesses = new();
    private readonly Mock<IStaffRepository> _staff = new();
    private readonly Mock<IAuditLogRepository> _audit = new();

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _reservationId = Guid.NewGuid();

    private ReservationManagementService CreateService() =>
        new(_reservations.Object, _businesses.Object, _staff.Object, _audit.Object);

    private void SetupReservation(Guid? reservationUserId = null)
    {
        _reservations.Setup(r => r.GetByIdAsync(_reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Reservation
            {
                Id = _reservationId, BusinessId = _businessId, ServiceId = Guid.NewGuid(), StaffId = Guid.NewGuid(),
                UserId = reservationUserId, GuestId = reservationUserId is null ? Guid.NewGuid() : null,
                Status = "pending", StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow.AddMinutes(30),
            });
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = _businessId, OwnerId = _ownerId, TierId = Guid.NewGuid(), Name = "Biz" });
        _audit.Setup(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _reservations.Setup(r => r.DeleteAsync(_reservationId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task CancelAsync_AsOwner_AuditsThenHardDeletes()
    {
        SetupReservation();
        AuditLog? logged = null;
        _audit.Setup(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLog, CancellationToken>((a, _) => logged = a).Returns(Task.CompletedTask);

        await CreateService().CancelAsync(_reservationId, _ownerId, "no puedo asistir");

        Assert.NotNull(logged);
        Assert.Equal("cancelled", logged!.Action);
        Assert.Equal(_reservationId, logged.ReservationId);
        Assert.Equal(_ownerId, logged.ActorId);
        Assert.NotNull(logged.OldValues); // snapshot de la reserva
        _reservations.Verify(r => r.DeleteAsync(_reservationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_AsReservationUser_IsAllowed()
    {
        var customerId = Guid.NewGuid();
        SetupReservation(reservationUserId: customerId);

        await CreateService().CancelAsync(_reservationId, customerId, null);

        _reservations.Verify(r => r.DeleteAsync(_reservationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_AsStaffOfBusiness_IsAllowed()
    {
        SetupReservation();
        var employeeId = Guid.NewGuid();
        _staff.Setup(s => s.ExistsForUserAsync(employeeId, _businessId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await CreateService().CancelAsync(_reservationId, employeeId, null);

        _reservations.Verify(r => r.DeleteAsync(_reservationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_Unauthorized_Throws_AndDoesNotDelete()
    {
        SetupReservation();
        _staff.Setup(s => s.ExistsForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<ReservationForbiddenException>(
            () => CreateService().CancelAsync(_reservationId, Guid.NewGuid(), null));

        _reservations.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _audit.Verify(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelAsync_NotFound_Throws()
    {
        _reservations.Setup(r => r.GetByIdAsync(_reservationId, It.IsAny<CancellationToken>())).ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(
            () => CreateService().CancelAsync(_reservationId, _ownerId, null));
    }
}
