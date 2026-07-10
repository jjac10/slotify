using Moq;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Disponibilidad por mes para el calendario del wizard (días en verde/rojo):
/// cada día del mes sale como 'closed' (sin horario o festivo de día completo),
/// 'full' (abierto pero sin huecos libres) o 'available' (quedan huecos).
/// Carga horario/festivos una vez y las reservas del mes en una sola consulta.
/// </summary>
public class MonthAvailabilityTests
{
    private readonly Mock<IBusinessRepository> _businesses = new();
    private readonly Mock<IServiceRepository> _services = new();
    private readonly Mock<IStaffRepository> _staff = new();
    private readonly Mock<IBusinessHourRepository> _hours = new();
    private readonly Mock<IBusinessHolidayRepository> _holidays = new();
    private readonly Mock<IReservationRepository> _reservations = new();

    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _serviceId = Guid.NewGuid();
    private readonly Guid _staffId = Guid.NewGuid();

    // Junio de 2026: los lunes son 1, 8, 15, 22 y 29. Horario de verano (CEST, UTC+2).
    private const int Year = 2026;
    private const int Month = 6;
    private const int MadridSummerOffsetHours = 2;

    private AvailabilityService CreateService() =>
        new(_businesses.Object, _services.Object, _staff.Object, _hours.Object, _holidays.Object, _reservations.Object);

    private void Setup(IEnumerable<BusinessHour> weeklyHours,
        IEnumerable<BusinessHoliday>? holidays = null, IEnumerable<Reservation>? reservations = null)
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = _businessId, OwnerId = Guid.NewGuid(), TierId = Guid.NewGuid(), Name = "Biz" });
        _services.Setup(s => s.GetByIdAsync(_serviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Service { Id = _serviceId, BusinessId = _businessId, Name = "Corte", DurationMinutes = 60 });
        _staff.Setup(s => s.GetByIdAsync(_staffId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Staff { Id = _staffId, BusinessId = _businessId, Role = "owner", Name = "O" });
        _hours.Setup(h => h.ListByBusinessAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(weeklyHours.ToList());
        _holidays.Setup(h => h.ListByBusinessAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((holidays ?? []).ToList());
        _reservations.Setup(r => r.ListByStaffBetweenAsync(
                _staffId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((reservations ?? []).ToList());
    }

    private static BusinessHour OpenMonday() =>
        new() { DayOfWeek = 1, IsClosed = false, OpeningTime = new TimeOnly(9, 0), ClosingTime = new TimeOnly(12, 0) };

    /// <summary>Reserva ocupando una hora local de Madrid del día indicado.</summary>
    private Reservation Occupied(int day, int localHour) => new()
    {
        Id = Guid.NewGuid(),
        BusinessId = _businessId,
        StaffId = _staffId,
        StartTime = new DateTime(Year, Month, day, localHour - MadridSummerOffsetHours, 0, 0, DateTimeKind.Utc),
        EndTime = new DateTime(Year, Month, day, localHour + 1 - MadridSummerOffsetHours, 0, 0, DateTimeKind.Utc),
        Status = "confirmed",
    };

    [Fact]
    public async Task GetMonth_ReturnsOneStatusPerDay_OpenDaysAvailable_RestClosed()
    {
        Setup([OpenMonday()]); // solo los lunes abiertos

        var days = await CreateService().GetMonthAsync(_businessId, _serviceId, _staffId, Year, Month);

        Assert.Equal(30, days.Count); // junio tiene 30 días
        var byDate = days.ToDictionary(d => d.Date, d => d.Status);
        foreach (var monday in new[] { 1, 8, 15, 22, 29 })
            Assert.Equal("available", byDate[new DateOnly(Year, Month, monday)]);
        Assert.Equal("closed", byDate[new DateOnly(Year, Month, 2)]);  // martes sin horario
        Assert.Equal("closed", byDate[new DateOnly(Year, Month, 7)]);  // domingo
    }

    [Fact]
    public async Task GetMonth_FullDayHoliday_MarksDayClosed()
    {
        Setup([OpenMonday()],
            holidays: [new BusinessHoliday { HolidayDate = new DateOnly(Year, Month, 8), IsClosed = true }]);

        var days = await CreateService().GetMonthAsync(_businessId, _serviceId, _staffId, Year, Month);

        var byDate = days.ToDictionary(d => d.Date, d => d.Status);
        Assert.Equal("closed", byDate[new DateOnly(Year, Month, 8)]);
        Assert.Equal("available", byDate[new DateOnly(Year, Month, 15)]); // el resto de lunes, intactos
    }

    [Fact]
    public async Task GetMonth_DayFullyBooked_MarksDayFull()
    {
        // Lunes 15: reservas 9-10, 10-11 y 11-12 (hora local) → sin huecos.
        Setup([OpenMonday()],
            reservations: [Occupied(15, 9), Occupied(15, 10), Occupied(15, 11)]);

        var days = await CreateService().GetMonthAsync(_businessId, _serviceId, _staffId, Year, Month);

        var byDate = days.ToDictionary(d => d.Date, d => d.Status);
        Assert.Equal("full", byDate[new DateOnly(Year, Month, 15)]);
        Assert.Equal("available", byDate[new DateOnly(Year, Month, 22)]);
    }

    [Fact]
    public async Task GetMonth_PartiallyBookedDay_StaysAvailable()
    {
        Setup([OpenMonday()], reservations: [Occupied(15, 9)]); // quedan 2 de 3 (> 1/3)

        var days = await CreateService().GetMonthAsync(_businessId, _serviceId, _staffId, Year, Month);

        Assert.Equal("available", days.Single(d => d.Date == new DateOnly(Year, Month, 15)).Status);
    }

    [Fact]
    public async Task GetMonth_FewSlotsLeft_MarksDayAlmostFull()
    {
        // Lunes 15: reservado 9-10 y 10-11 → queda 1 hueco de 3 (≤ 1/3 de la capacidad).
        Setup([OpenMonday()], reservations: [Occupied(15, 9), Occupied(15, 10)]);

        var days = await CreateService().GetMonthAsync(_businessId, _serviceId, _staffId, Year, Month);

        var byDate = days.ToDictionary(d => d.Date, d => d.Status);
        Assert.Equal("almost_full", byDate[new DateOnly(Year, Month, 15)]);
        Assert.Equal("available", byDate[new DateOnly(Year, Month, 22)]);
    }

    [Fact]
    public async Task GetMonth_NowUtcSkipsPastSlots_TodayWithoutRemainingSlotsIsFull()
    {
        Setup([OpenMonday()]);
        // "Ahora" = lunes 15 a las 13:00 local (11:00 UTC): ya no queda hueco ese día.
        var nowUtc = new DateTime(Year, Month, 15, 11, 0, 0, DateTimeKind.Utc);

        var days = await CreateService().GetMonthAsync(_businessId, _serviceId, _staffId, Year, Month, nowUtc);

        var byDate = days.ToDictionary(d => d.Date, d => d.Status);
        Assert.Equal("full", byDate[new DateOnly(Year, Month, 15)]);
        Assert.Equal("available", byDate[new DateOnly(Year, Month, 22)]); // los lunes futuros siguen libres
    }

    [Fact]
    public async Task GetMonth_QueriesReservationsOnceForTheWholeMonth()
    {
        Setup([OpenMonday()]);

        await CreateService().GetMonthAsync(_businessId, _serviceId, _staffId, Year, Month);

        _reservations.Verify(r => r.ListByStaffBetweenAsync(
            _staffId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _reservations.Verify(r => r.ListByStaffOnDateAsync(
            It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
