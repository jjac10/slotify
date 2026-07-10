using Moq;
using Slotify.Domain.DTOs;
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

    // --- No asistió (POST /no-show) --------------------------------------------

    /// <summary>Deja la reserva como cita pasada activa (empezó hace 2 horas).</summary>
    private void MakeReservationPast(string status = "confirmed")
    {
        _reservation.Status = status;
        _reservation.StartTime = DateTime.UtcNow.AddHours(-2);
        _reservation.EndTime = DateTime.UtcNow.AddHours(-1);
    }

    [Fact]
    public async Task MarkNoShowAsync_AsOwner_OnPastReservation_SetsNoShow_BumpsVersion_AndAudits()
    {
        SetupReservation();
        MakeReservationPast();
        AuditLog? logged = null;
        _audit.Setup(a => a.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Callback<AuditLog, CancellationToken>((a, _) => logged = a).Returns(Task.CompletedTask);

        var result = await CreateService().MarkNoShowAsync(_reservationId, _ownerId);

        Assert.Equal("no-show", _reservation.Status);
        Assert.Equal("no-show", result.Status);
        Assert.Equal(1, _reservation.Version);
        _reservations.Verify(r => r.UpdateAsync(_reservation, It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(logged);
        Assert.Equal("no-show", logged!.Action);
        Assert.Equal("owner", logged.ActorType);
    }

    [Fact]
    public async Task MarkNoShowAsync_ReservationNotStartedYet_Throws_AndDoesNotUpdate()
    {
        SetupReservation(); // empieza dentro de 30 días

        await Assert.ThrowsAsync<ReservationNotPastException>(
            () => CreateService().MarkNoShowAsync(_reservationId, _ownerId));

        _reservations.Verify(r => r.UpdateAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkNoShowAsync_AsReservationCustomer_Throws()
    {
        var customerId = Guid.NewGuid();
        SetupReservation(reservationUserId: customerId);
        MakeReservationPast();
        _staff.Setup(s => s.ExistsForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<ReservationForbiddenException>(
            () => CreateService().MarkNoShowAsync(_reservationId, customerId));
    }

    [Fact]
    public async Task MarkNoShowAsync_AlreadyNoShow_Throws_AndDoesNotUpdate()
    {
        SetupReservation();
        MakeReservationPast(status: "no-show");

        await Assert.ThrowsAsync<ReservationNotPendingException>(
            () => CreateService().MarkNoShowAsync(_reservationId, _ownerId));

        _reservations.Verify(r => r.UpdateAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
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
    public async Task ListMineAsync_ReturnsPagedCurrentUsersReservations_WithDefaults()
    {
        var userId = Guid.NewGuid();
        IReadOnlyList<Reservation> items = [SampleReservation(_businessId, userId)];
        _guests.Setup(g => g.ListIdsByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        // Defaults: scope=all, page=1 (skip 0), pageSize=20.
        _reservations.Setup(r => r.ListByUserAsync(userId, It.IsAny<IReadOnlyCollection<Guid>>(),
                ReservationScope.All, It.IsAny<DateTime>(), 0, ReservationManagementService.DefaultPageSize,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 1));

        var result = await CreateService().ListMineAsync(userId);

        Assert.Single(result.Items);
        Assert.Equal(userId, result.Items[0].UserId);
        Assert.Equal(1, result.Total);
        Assert.Equal(1, result.Page);
        Assert.Equal(ReservationManagementService.DefaultPageSize, result.PageSize);
    }

    [Fact]
    public async Task ListMineAsync_PassesLinkedGuestIds_ToASinglePagedQuery()
    {
        // Reservas que hizo como invitado y se vincularon a la cuenta al registrarse:
        // van en la MISMA consulta paginada (guestIds), no en una segunda en memoria.
        var userId = Guid.NewGuid();
        var guestId = Guid.NewGuid();
        var direct = SampleReservation(_businessId, userId);
        var viaGuest = SampleReservation(_businessId, null); // reserva de invitado (GuestId, sin UserId)
        IReadOnlyList<Reservation> items = [direct, viaGuest];
        _guests.Setup(g => g.ListIdsByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([guestId]);
        _reservations.Setup(r => r.ListByUserAsync(userId,
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(guestId)),
                ReservationScope.All, It.IsAny<DateTime>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 2));

        var result = await CreateService().ListMineAsync(userId);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.Total);
        Assert.Contains(result.Items, r => r.Id == direct.Id);
        Assert.Contains(result.Items, r => r.Id == viaGuest.Id);
    }

    [Theory]
    [InlineData(ReservationScope.All)]
    [InlineData(ReservationScope.Upcoming)]
    [InlineData(ReservationScope.Past)]
    public async Task ListMineAsync_PassesScopeAndSkipTake_ToRepository(ReservationScope scope)
    {
        var userId = Guid.NewGuid();
        IReadOnlyList<Reservation> empty = [];
        _guests.Setup(g => g.ListIdsByUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _reservations.Setup(r => r.ListByUserAsync(userId, It.IsAny<IReadOnlyCollection<Guid>>(),
                scope, It.IsAny<DateTime>(), 10, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((empty, 12));

        // page=3, pageSize=5 → skip 10, take 5. El "ahora" (UTC) lo fija el servicio.
        var result = await CreateService().ListMineAsync(userId, scope, page: 3, pageSize: 5);

        Assert.Equal(3, result.Page);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(12, result.Total);
        _reservations.Verify(r => r.ListByUserAsync(userId, It.IsAny<IReadOnlyCollection<Guid>>(),
            scope, It.Is<DateTime>(d => d.Kind == DateTimeKind.Utc), 10, 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0, 20)]   // page < 1
    [InlineData(-3, 20)]  // page negativo
    [InlineData(1, 0)]    // pageSize < 1
    [InlineData(1, 51)]   // pageSize > máximo (50)
    public async Task ListMineAsync_InvalidPagination_Throws_AndDoesNotQuery(int page, int pageSize)
    {
        await Assert.ThrowsAsync<InvalidPaginationException>(
            () => CreateService().ListMineAsync(Guid.NewGuid(), ReservationScope.All, page, pageSize));

        _reservations.Verify(r => r.ListByUserAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(),
            It.IsAny<ReservationScope>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ListForBusinessAsync_AsOwner_ReturnsPagedList_WithDefaults()
    {
        SetupBusiness();
        IReadOnlyList<Reservation> items = [SampleReservation(_businessId)];
        _reservations.Setup(r => r.ListByBusinessAsync(_businessId, null, null,
                0, ReservationManagementService.DefaultPageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 1));

        var result = await CreateService().ListForBusinessAsync(_businessId, _ownerId, null, null);

        Assert.Single(result.Items);
        Assert.Equal(1, result.Total);
        Assert.Equal(1, result.Page);
        Assert.Equal(ReservationManagementService.DefaultPageSize, result.PageSize);
    }

    [Fact]
    public async Task ListForBusinessAsync_PassesSkipTake_ToRepository()
    {
        SetupBusiness();
        IReadOnlyList<Reservation> empty = [];
        _reservations.Setup(r => r.ListByBusinessAsync(_businessId, null, null, 4, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((empty, 9));

        // page=3, pageSize=2 → skip 4, take 2; el total del filtro viaja en la respuesta.
        var result = await CreateService().ListForBusinessAsync(_businessId, _ownerId, null, null, page: 3, pageSize: 2);

        Assert.Equal(9, result.Total);
        Assert.Equal(3, result.Page);
        Assert.Equal(2, result.PageSize);
    }

    [Fact]
    public async Task ListForBusinessAsync_AsStaff_ScopesToOwnReservations()
    {
        var employeeUserId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var date = new DateOnly(2026, 7, 1);
        SetupBusiness();
        // El empleado pertenece al negocio; su agenda se acota a su propio staffId.
        _staff.Setup(s => s.GetByUserAsync(employeeUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Staff { Id = staffId, BusinessId = _businessId, UserId = employeeUserId, Role = "employee", Name = "E" });
        IReadOnlyList<Reservation> empty = [];
        _reservations.Setup(r => r.ListByBusinessAsync(_businessId, date, staffId,
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((empty, 0));

        // Aunque pase otro staffFilter, se ignora y se usa el suyo.
        var result = await CreateService().ListForBusinessAsync(_businessId, employeeUserId, date, Guid.NewGuid());

        Assert.Empty(result.Items);
        _reservations.Verify(r => r.ListByBusinessAsync(_businessId, date, staffId,
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListForBusinessAsync_Unauthorized_Throws_AndDoesNotQuery()
    {
        SetupBusiness();
        _staff.Setup(s => s.ExistsForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<ReservationForbiddenException>(
            () => CreateService().ListForBusinessAsync(_businessId, Guid.NewGuid(), null, null));

        _reservations.Verify(r => r.ListByBusinessAsync(It.IsAny<Guid>(), It.IsAny<DateOnly?>(), It.IsAny<Guid?>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public async Task ListForBusinessAsync_InvalidPagination_Throws_AndDoesNotQuery(int page, int pageSize)
    {
        SetupBusiness();

        await Assert.ThrowsAsync<InvalidPaginationException>(
            () => CreateService().ListForBusinessAsync(_businessId, _ownerId, null, null, page, pageSize));

        _reservations.Verify(r => r.ListByBusinessAsync(It.IsAny<Guid>(), It.IsAny<DateOnly?>(), It.IsAny<Guid?>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
