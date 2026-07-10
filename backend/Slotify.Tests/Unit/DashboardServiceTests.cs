using Moq;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Resumen del panel del owner: agrega contadores + ingresos del mes + próximas
/// reservas + métricas avanzadas (tasa de no-shows y ocupación del mes en curso).
/// Autorización owner-only (404 si no existe, 403 si no es el dueño).
/// </summary>
public class DashboardServiceTests
{
    private readonly Mock<IReservationRepository> _reservations = new();
    private readonly Mock<IBusinessRepository> _businesses = new();
    private readonly Mock<IReviewRepository> _reviews = new();
    private readonly Mock<IBusinessHourRepository> _hours = new();
    private readonly Mock<IBusinessHolidayRepository> _holidays = new();
    private readonly Mock<IStaffRepository> _staff = new();

    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    public DashboardServiceTests()
    {
        // Defaults inocuos para las métricas avanzadas (los tests que las miden los pisan).
        _hours.Setup(h => h.ListByBusinessAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BusinessHour>());
        _holidays.Setup(h => h.ListByBusinessAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BusinessHoliday>());
        _staff.Setup(s => s.CountByBusinessAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _reservations.Setup(r => r.ListUpcomingByBusinessAsync(_businessId, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Reservation>());
        _reviews.Setup(r => r.ListByBusinessAsync(_businessId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Review>(), 0));
    }

    private DashboardService CreateService() => new(
        _reservations.Object, _businesses.Object, _reviews.Object,
        _hours.Object, _holidays.Object, _staff.Object);

    private void SetupBusinessOwnedBy(Guid ownerId, double? rating = null, int reviewCount = 0)
        => _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = _businessId, OwnerId = ownerId, Name = "Barbería", Rating = rating, ReviewCount = reviewCount });

    [Fact]
    public async Task GetAsync_ForOwner_AggregatesMetricsAndMapsUpcoming()
    {
        SetupBusinessOwnedBy(_ownerId, rating: 4.5, reviewCount: 10);
        _reviews.Setup(r => r.ListByBusinessAsync(_businessId, 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Review>
            {
                new() { Id = Guid.NewGuid(), BusinessId = _businessId, UserId = Guid.NewGuid(), ReservationId = Guid.NewGuid(), Rating = 5, Comment = "Top", CreatedAt = DateTime.UtcNow },
            }, 1));
        var now = new DateTime(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc);
        var monthStart = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        _reservations.Setup(r => r.CountByBusinessAsync(_businessId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
        _reservations.Setup(r => r.CountByBusinessAsync(_businessId, monthStart, monthEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);
        _reservations.Setup(r => r.SumRevenueByBusinessAsync(_businessId, monthStart, monthEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(175m);
        var upcomingId = Guid.NewGuid();
        _reservations.Setup(r => r.ListUpcomingByBusinessAsync(_businessId, now, DashboardService.UpcomingLimit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Reservation>
            {
                new()
                {
                    Id = upcomingId, BusinessId = _businessId, ServiceId = Guid.NewGuid(), StaffId = Guid.NewGuid(),
                    StartTime = now.AddHours(2), EndTime = now.AddHours(2).AddMinutes(30), Status = "pending",
                },
            });

        var result = await CreateService().GetAsync(_businessId, _ownerId, now);

        Assert.Equal(42, result.TotalReservations);
        Assert.Equal(7, result.ReservationsThisMonth);
        Assert.Equal(175m, result.EstimatedMonthlyRevenue);
        Assert.Single(result.UpcomingReservations);
        Assert.Equal(upcomingId, result.UpcomingReservations[0].Id);
        Assert.Equal(4.5, result.AverageRating);
        Assert.Equal(10, result.ReviewCount);
        Assert.Single(result.RecentReviews);
        Assert.Equal(5, result.RecentReviews[0].Rating);
    }

    // --- Métricas avanzadas: no-shows y ocupación del mes ---------------------

    [Fact]
    public async Task GetAsync_ComputesNoShowRate_OverPastReservationsOfTheMonth()
    {
        SetupBusinessOwnedBy(_ownerId);
        var now = new DateTime(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc);
        var monthStart = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // 4 reservas ya pasadas este mes, 1 de ellas marcada no-show → 25 %.
        _reservations.Setup(r => r.CountByBusinessAsync(_businessId, monthStart, now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);
        _reservations.Setup(r => r.CountNoShowsByBusinessAsync(_businessId, monthStart, now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await CreateService().GetAsync(_businessId, _ownerId, now);

        Assert.Equal(1, result.NoShowsThisMonth);
        Assert.Equal(0.25, result.NoShowRate);
    }

    [Fact]
    public async Task GetAsync_NoPastReservationsThisMonth_NoShowRateIsNull()
    {
        SetupBusinessOwnedBy(_ownerId);

        var result = await CreateService().GetAsync(_businessId, _ownerId, DateTime.UtcNow);

        Assert.Equal(0, result.NoShowsThisMonth);
        Assert.Null(result.NoShowRate);
    }

    [Fact]
    public async Task GetAsync_ComputesOccupancy_ReservedMinutesOverOpenCapacityMonthToDate()
    {
        SetupBusinessOwnedBy(_ownerId);
        // 17 de junio de 2026 (miércoles). Solo los lunes abiertos 9–17 (480 min locales).
        // Lunes transcurridos del mes: 1, 8 y 15 → 1440 min × 1 staff de capacidad.
        var now = new DateTime(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc);
        _hours.Setup(h => h.ListByBusinessAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BusinessHour>
            {
                new() { DayOfWeek = 1, IsClosed = false, OpeningTime = new TimeOnly(9, 0), ClosingTime = new TimeOnly(17, 0) },
            });
        _reservations.Setup(r => r.SumReservedMinutesAsync(
                _businessId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(360); // 6 h reservadas

        var result = await CreateService().GetAsync(_businessId, _ownerId, now);

        Assert.Equal(0.25, result.OccupancyRate); // 360 / 1440
    }

    [Fact]
    public async Task GetAsync_FullDayHolidayRemovesThatDayFromCapacity()
    {
        SetupBusinessOwnedBy(_ownerId);
        var now = new DateTime(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc);
        _hours.Setup(h => h.ListByBusinessAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BusinessHour>
            {
                new() { DayOfWeek = 1, IsClosed = false, OpeningTime = new TimeOnly(9, 0), ClosingTime = new TimeOnly(17, 0) },
            });
        // El lunes 8 fue festivo de día completo → capacidad = 2 lunes × 480 = 960.
        _holidays.Setup(h => h.ListByBusinessAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BusinessHoliday>
            {
                new() { HolidayDate = new DateOnly(2026, 6, 8), IsClosed = true },
            });
        _reservations.Setup(r => r.SumReservedMinutesAsync(
                _businessId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(480);

        var result = await CreateService().GetAsync(_businessId, _ownerId, now);

        Assert.Equal(0.5, result.OccupancyRate); // 480 / 960
    }

    [Fact]
    public async Task GetAsync_NoOpenCapacity_OccupancyIsNull()
    {
        SetupBusinessOwnedBy(_ownerId); // sin horario configurado (default del arnés)

        var result = await CreateService().GetAsync(_businessId, _ownerId, DateTime.UtcNow);

        Assert.Null(result.OccupancyRate);
    }

    [Fact]
    public async Task GetAsync_WhenBusinessDoesNotExist_ThrowsBusinessNotFound()
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Business?)null);

        await Assert.ThrowsAsync<BusinessNotFoundException>(
            () => CreateService().GetAsync(_businessId, _ownerId, DateTime.UtcNow));
    }

    [Fact]
    public async Task GetAsync_WhenCallerIsNotOwner_ThrowsNotBusinessOwner()
    {
        SetupBusinessOwnedBy(Guid.NewGuid()); // pertenece a otro

        await Assert.ThrowsAsync<NotBusinessOwnerException>(
            () => CreateService().GetAsync(_businessId, _ownerId, DateTime.UtcNow));
    }
}
