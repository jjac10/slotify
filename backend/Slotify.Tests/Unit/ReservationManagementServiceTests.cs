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
    private readonly Mock<IBlindIndex> _blindIndex = new();
    private readonly Mock<IGuestRepository> _guests = new();

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _reservationId = Guid.NewGuid();

    // Fija el inicio bien en el futuro para que la ventana de antelación no aplique
    // salvo en los tests que la configuran explícitamente.
    private static readonly DateTime At10 = DateTime.UtcNow.AddDays(30);
    private Reservation _reservation = null!;

    private ReservationManagementService CreateService() =>
        new(_reservations.Object, _businesses.Object, _staff.Object, _audit.Object, _blindIndex.Object, _guests.Object);

    private void SetupReservation(Guid? reservationUserId = null)
    {
        _reservation = new Reservation
        {
            Id = _reservationId, BusinessId = _businessId, ServiceId = Guid.NewGuid(), StaffId = Guid.NewGuid(),
            UserId = reservationUserId, GuestId = reservationUserId is null ? Guid.NewGuid() : null,
            Status = "pending", StartTime = At10, EndTime = At10.AddMinutes(30),
        };
        _reservations.Setup(r => r.GetByIdAsync(_reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_reservation);
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = _businessId, OwnerId = _ownerId, TierId = Guid.NewGuid(), Name = "Biz" });
        _audit.Setup(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _reservations.Setup(r => r.DeleteAsync(_reservationId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _reservations.Setup(r => r.UpdateAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
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

    // --- Reprogramar (PATCH) -------------------------------------------------

    [Fact]
    public async Task RescheduleAsync_AsOwner_MovesTime_PreservesDuration_BumpsVersion_AndAudits()
    {
        SetupReservation();
        AuditLog? logged = null;
        _audit.Setup(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLog, CancellationToken>((a, _) => logged = a).Returns(Task.CompletedTask);
        var newStart = At10.AddHours(2);

        var result = await CreateService().RescheduleAsync(_reservationId, _ownerId, newStart);

        // Conserva la duración (30 min) y recalcula el fin.
        Assert.Equal(newStart, _reservation.StartTime);
        Assert.Equal(newStart.AddMinutes(30), _reservation.EndTime);
        Assert.Equal(newStart, result.StartTime);
        Assert.Equal(newStart.AddMinutes(30), result.EndTime);
        // Optimistic locking: la versión se incrementa.
        Assert.Equal(1, _reservation.Version);
        _reservations.Verify(r => r.UpdateAsync(_reservation, It.IsAny<CancellationToken>()), Times.Once);
        // Auditoría action='updated' con snapshot antiguo y nuevo.
        Assert.NotNull(logged);
        Assert.Equal("updated", logged!.Action);
        Assert.Equal(_reservationId, logged.ReservationId);
        Assert.Equal(_ownerId, logged.ActorId);
        Assert.NotNull(logged.OldValues);
        Assert.NotNull(logged.NewValues);
    }

    [Fact]
    public async Task RescheduleAsync_AsReservationUser_IsAllowed()
    {
        var customerId = Guid.NewGuid();
        SetupReservation(reservationUserId: customerId);

        await CreateService().RescheduleAsync(_reservationId, customerId, At10.AddHours(1));

        _reservations.Verify(r => r.UpdateAsync(_reservation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RescheduleAsync_Unauthorized_Throws_AndDoesNotUpdate()
    {
        SetupReservation();
        _staff.Setup(s => s.ExistsForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<ReservationForbiddenException>(
            () => CreateService().RescheduleAsync(_reservationId, Guid.NewGuid(), At10.AddHours(1)));

        _reservations.Verify(r => r.UpdateAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
        _audit.Verify(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RescheduleAsync_OverlapsAnotherReservation_ThrowsSlotUnavailable_AndDoesNotUpdate()
    {
        SetupReservation();
        // El pre-check excluye la propia reserva y aun así encuentra solape con otra.
        _reservations.Setup(r => r.HasOverlapAsync(
            _reservation.StaffId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), _reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<SlotUnavailableException>(
            () => CreateService().RescheduleAsync(_reservationId, _ownerId, At10.AddHours(1)));

        _reservations.Verify(r => r.UpdateAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
        _audit.Verify(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RescheduleAsync_NotFound_Throws()
    {
        _reservations.Setup(r => r.GetByIdAsync(_reservationId, It.IsAny<CancellationToken>())).ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(
            () => CreateService().RescheduleAsync(_reservationId, _ownerId, At10.AddHours(1)));
    }

    // --- Confirmar (POST /confirm) -------------------------------------------

    [Fact]
    public async Task ConfirmAsync_AsOwner_SetsConfirmed_BumpsVersion_AndAudits()
    {
        SetupReservation(); // status pending
        AuditLog? logged = null;
        _audit.Setup(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLog, CancellationToken>((a, _) => logged = a).Returns(Task.CompletedTask);

        var result = await CreateService().ConfirmAsync(_reservationId, _ownerId);

        Assert.Equal("confirmed", _reservation.Status);
        Assert.Equal("confirmed", result.Status);
        Assert.Equal(1, _reservation.Version);
        _reservations.Verify(r => r.UpdateAsync(_reservation, It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(logged);
        Assert.Equal("confirmed", logged!.Action);
        Assert.Equal(_ownerId, logged.ActorId);
        Assert.Equal("owner", logged.ActorType);
    }

    [Fact]
    public async Task ConfirmAsync_AsStaffOfBusiness_IsAllowed()
    {
        SetupReservation();
        var employeeId = Guid.NewGuid();
        _staff.Setup(s => s.ExistsForUserAsync(employeeId, _businessId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateService().ConfirmAsync(_reservationId, employeeId);

        Assert.Equal("confirmed", result.Status);
        _reservations.Verify(r => r.UpdateAsync(_reservation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmAsync_AsReservationCustomer_Throws_AndDoesNotUpdate()
    {
        // El cliente de la reserva NO puede confirmar: es acción del negocio.
        var customerId = Guid.NewGuid();
        SetupReservation(reservationUserId: customerId);
        _staff.Setup(s => s.ExistsForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<ReservationForbiddenException>(
            () => CreateService().ConfirmAsync(_reservationId, customerId));

        _reservations.Verify(r => r.UpdateAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
        _audit.Verify(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmAsync_AlreadyConfirmed_ThrowsNotPending_AndDoesNotUpdate()
    {
        SetupReservation();
        _reservation.Status = "confirmed";

        await Assert.ThrowsAsync<ReservationNotPendingException>(
            () => CreateService().ConfirmAsync(_reservationId, _ownerId));

        _reservations.Verify(r => r.UpdateAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
        _audit.Verify(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmAsync_NotFound_Throws()
    {
        _reservations.Setup(r => r.GetByIdAsync(_reservationId, It.IsAny<CancellationToken>())).ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(
            () => CreateService().ConfirmAsync(_reservationId, _ownerId));
    }

    // --- Ventana de antelación (cutoff) --------------------------------------

    private void SetupBusinessWithCutoff(int hours) =>
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = _businessId, OwnerId = _ownerId, TierId = Guid.NewGuid(), Name = "Biz", CancellationCutoffHours = hours });

    [Fact]
    public async Task CancelAsync_AsClientWithinCutoffWindow_Throws_AndDoesNotDelete()
    {
        var customerId = Guid.NewGuid();
        SetupReservation(reservationUserId: customerId);
        _reservation.StartTime = DateTime.UtcNow.AddHours(1); // dentro de la ventana de 24 h
        _reservation.EndTime = _reservation.StartTime.AddMinutes(30);
        SetupBusinessWithCutoff(24);

        await Assert.ThrowsAsync<CancellationWindowClosedException>(
            () => CreateService().CancelAsync(_reservationId, customerId, null));

        _reservations.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelAsync_AsOwnerWithinCutoffWindow_IsAllowed()
    {
        SetupReservation();
        _reservation.StartTime = DateTime.UtcNow.AddHours(1);
        _reservation.EndTime = _reservation.StartTime.AddMinutes(30);
        SetupBusinessWithCutoff(24); // el owner no está sujeto a la ventana

        await CreateService().CancelAsync(_reservationId, _ownerId, null);

        _reservations.Verify(r => r.DeleteAsync(_reservationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RescheduleAsync_AsClientWithinCutoffWindow_Throws_AndDoesNotUpdate()
    {
        var customerId = Guid.NewGuid();
        SetupReservation(reservationUserId: customerId);
        _reservation.StartTime = DateTime.UtcNow.AddHours(1);
        _reservation.EndTime = _reservation.StartTime.AddMinutes(30);
        SetupBusinessWithCutoff(24);

        await Assert.ThrowsAsync<CancellationWindowClosedException>(
            () => CreateService().RescheduleAsync(_reservationId, customerId, _reservation.StartTime.AddDays(1)));

        _reservations.Verify(r => r.UpdateAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Invitado (cancelar/reprogramar sin cuenta, por contacto) ------------

    [Fact]
    public async Task CancelAsGuestAsync_WithMatchingContact_Cancels()
    {
        SetupReservation(); // reserva de invitado (GuestId no nulo)
        _blindIndex.Setup(b => b.Compute(It.IsAny<string>())).Returns("HASH");
        _guests.Setup(g => g.FindIdsByContactHashAsync("HASH", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _reservation.GuestId!.Value });

        await CreateService().CancelAsGuestAsync(_reservationId, "+34600000000", null);

        _reservations.Verify(r => r.DeleteAsync(_reservationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsGuestAsync_WithWrongContact_Throws_AndDoesNotDelete()
    {
        SetupReservation();
        _blindIndex.Setup(b => b.Compute(It.IsAny<string>())).Returns("OTHER");
        _guests.Setup(g => g.FindIdsByContactHashAsync("OTHER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        await Assert.ThrowsAsync<ReservationForbiddenException>(
            () => CreateService().CancelAsGuestAsync(_reservationId, "+34699999999", null));

        _reservations.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelAsGuestAsync_OnUserReservation_Throws()
    {
        SetupReservation(reservationUserId: Guid.NewGuid()); // reserva de usuario, no de invitado

        await Assert.ThrowsAsync<ReservationForbiddenException>(
            () => CreateService().CancelAsGuestAsync(_reservationId, "+34600000000", null));
    }

    [Fact]
    public async Task RescheduleAsGuestAsync_WithMatchingContact_MovesTime()
    {
        SetupReservation();
        _blindIndex.Setup(b => b.Compute(It.IsAny<string>())).Returns("HASH");
        _guests.Setup(g => g.FindIdsByContactHashAsync("HASH", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _reservation.GuestId!.Value });
        var newStart = _reservation.StartTime.AddHours(2);

        var result = await CreateService().RescheduleAsGuestAsync(_reservationId, "+34600000000", newStart);

        Assert.Equal(newStart, result.StartTime);
        _reservations.Verify(r => r.UpdateAsync(_reservation, It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Listados ------------------------------------------------------------

    private void SetupBusiness() =>
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = _businessId, OwnerId = _ownerId, TierId = Guid.NewGuid(), Name = "Biz" });

    private static Reservation SampleReservation(Guid businessId, Guid? userId = null) => new()
    {
        Id = Guid.NewGuid(), BusinessId = businessId, ServiceId = Guid.NewGuid(), StaffId = Guid.NewGuid(),
        UserId = userId, GuestId = userId is null ? Guid.NewGuid() : null,
        StartTime = At10, EndTime = At10.AddMinutes(30), Status = "pending",
    };

    [Fact]
    public async Task ListMineAsync_ReturnsCurrentUsersReservations()
    {
        var userId = Guid.NewGuid();
        _reservations.Setup(r => r.ListByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([SampleReservation(_businessId, userId)]);

        var result = await CreateService().ListMineAsync(userId);

        Assert.Single(result);
        Assert.Equal(userId, result[0].UserId);
    }

    [Fact]
    public async Task ListForBusinessAsync_AsOwner_ReturnsList()
    {
        SetupBusiness();
        _reservations.Setup(r => r.ListByBusinessAsync(_businessId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([SampleReservation(_businessId)]);

        var result = await CreateService().ListForBusinessAsync(_businessId, _ownerId, null, null);

        Assert.Single(result);
    }

    [Fact]
    public async Task ListForBusinessAsync_AsStaff_PassesFiltersThrough()
    {
        var employeeId = Guid.NewGuid();
        var date = new DateOnly(2026, 7, 1);
        var staffFilter = Guid.NewGuid();
        SetupBusiness();
        _staff.Setup(s => s.ExistsForUserAsync(employeeId, _businessId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _reservations.Setup(r => r.ListByBusinessAsync(_businessId, date, staffFilter, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateService().ListForBusinessAsync(_businessId, employeeId, date, staffFilter);

        Assert.Empty(result);
        _reservations.Verify(r => r.ListByBusinessAsync(_businessId, date, staffFilter, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListForBusinessAsync_Unauthorized_Throws_AndDoesNotQuery()
    {
        SetupBusiness();
        _staff.Setup(s => s.ExistsForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<ReservationForbiddenException>(
            () => CreateService().ListForBusinessAsync(_businessId, Guid.NewGuid(), null, null));

        _reservations.Verify(r => r.ListByBusinessAsync(
            It.IsAny<Guid>(), It.IsAny<DateOnly?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
