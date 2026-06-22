using Moq;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Cálculo de slots libres = horario − festivos − reservas, con paso configurable.
/// </summary>
public class AvailabilityServiceTests
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
    private static readonly DateOnly Date = new(2026, 6, 22); // lunes
    private static int Dow => (int)Date.DayOfWeek;

    private AvailabilityService CreateService() =>
        new(_businesses.Object, _services.Object, _staff.Object, _hours.Object, _holidays.Object, _reservations.Object);

    private void Setup(int duration, int? interval, BusinessHour dayHours,
        IEnumerable<BusinessHoliday>? holidays = null, IEnumerable<Reservation>? reservations = null)
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = _businessId, OwnerId = Guid.NewGuid(), TierId = Guid.NewGuid(), Name = "Biz", SlotIntervalMinutes = interval });
        _services.Setup(s => s.GetByIdAsync(_serviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Service { Id = _serviceId, BusinessId = _businessId, Name = "Corte", DurationMinutes = duration });
        _staff.Setup(s => s.GetByIdAsync(_staffId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Staff { Id = _staffId, BusinessId = _businessId, Role = "owner", Name = "O" });
        _hours.Setup(h => h.ListByBusinessAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { dayHours });
        _holidays.Setup(h => h.ListByBusinessAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((holidays ?? Array.Empty<BusinessHoliday>()).ToList());
        _reservations.Setup(r => r.ListByStaffOnDateAsync(_staffId, Date, It.IsAny<CancellationToken>()))
            .ReturnsAsync((reservations ?? Array.Empty<Reservation>()).ToList());
    }

    private static BusinessHour Open(int dow, int openH, int closeH) =>
        new() { DayOfWeek = dow, IsClosed = false, OpeningTime = new TimeOnly(openH, 0), ClosingTime = new TimeOnly(closeH, 0) };

    // El horario del negocio es hora local de Europe/Madrid; los slots salen en UTC.
    // Date (2026-06-22) cae en horario de verano (CEST, UTC+2) → UTC = local − 2h.
    private const int MadridSummerOffsetHours = 2;
    private static DateTime Utc(int localH, int m = 0) =>
        new(Date.Year, Date.Month, Date.Day, localH - MadridSummerOffsetHours, m, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetSlots_ClosedDay_ReturnsEmpty()
    {
        Setup(60, null, new BusinessHour { DayOfWeek = Dow, IsClosed = true });

        Assert.Empty(await CreateService().GetSlotsAsync(_businessId, _serviceId, _staffId, Date));
    }

    [Fact]
    public async Task GetSlots_Holiday_ReturnsEmpty()
    {
        Setup(60, null, Open(Dow, 9, 18),
            holidays: new[] { new BusinessHoliday { HolidayDate = Date, IsClosed = true } });

        Assert.Empty(await CreateService().GetSlotsAsync(_businessId, _serviceId, _staffId, Date));
    }

    [Fact]
    public async Task GetSlots_HolidayRangeCoversDate_ReturnsEmpty()
    {
        // Rango cerrado del día anterior al siguiente → cubre Date.
        Setup(60, null, Open(Dow, 9, 18),
            holidays: new[] { new BusinessHoliday { HolidayDate = Date.AddDays(-1), EndDate = Date.AddDays(1), IsClosed = true } });

        Assert.Empty(await CreateService().GetSlotsAsync(_businessId, _serviceId, _staffId, Date));
    }

    [Fact]
    public async Task GetSlots_HolidayRangeBeforeDate_DoesNotAffect()
    {
        // Rango que termina antes de Date → no cubre, hay slots normales.
        Setup(60, null, Open(Dow, 9, 12),
            holidays: new[] { new BusinessHoliday { HolidayDate = Date.AddDays(-3), EndDate = Date.AddDays(-1), IsClosed = true } });

        Assert.Equal(3, (await CreateService().GetSlotsAsync(_businessId, _serviceId, _staffId, Date)).Count);
    }

    [Fact]
    public async Task GetSlots_PartialHourClosure_RemovesOverlappingSlots()
    {
        // Abierto 9-12 (slots 9-10, 10-11, 11-12). Cierre parcial 10:00-11:00 (hora local).
        Setup(60, null, Open(Dow, 9, 12),
            holidays: new[] { new BusinessHoliday { HolidayDate = Date, IsClosed = true, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(11, 0) } });

        var slots = await CreateService().GetSlotsAsync(_businessId, _serviceId, _staffId, Date);

        Assert.Equal(2, slots.Count);                       // queda 9-10 y 11-12
        Assert.DoesNotContain(slots, s => s.Start == Utc(10)); // 10-11 eliminado
        Assert.Contains(slots, s => s.Start == Utc(9));
        Assert.Contains(slots, s => s.Start == Utc(11));
    }

    [Fact]
    public async Task GetSlots_OpenDay_StepDefaultsToDuration()
    {
        Setup(60, null, Open(Dow, 9, 12)); // 9-12, servicio 60 min, sin intervalo

        var slots = await CreateService().GetSlotsAsync(_businessId, _serviceId, _staffId, Date);

        Assert.Equal(3, slots.Count); // 9-10, 10-11, 11-12
        Assert.Equal(Utc(9), slots[0].Start);
        Assert.Equal(Utc(12), slots[^1].End);
    }

    [Fact]
    public async Task GetSlots_ConfiguredInterval_UsesStep()
    {
        Setup(30, interval: 15, Open(Dow, 9, 10)); // 9-10, servicio 30, paso 15

        var slots = await CreateService().GetSlotsAsync(_businessId, _serviceId, _staffId, Date);

        // inicios 9:00, 9:15, 9:30 (9:30+30=10:00 <= cierre)
        Assert.Equal(3, slots.Count);
        Assert.Equal(Utc(9, 0), slots[0].Start);
        Assert.Equal(Utc(9, 30), slots[^1].Start);
    }

    [Fact]
    public async Task GetSlots_WithNow_ExcludesPastSlots()
    {
        Setup(60, null, Open(Dow, 9, 12)); // inicios 9:00, 10:00, 11:00 (local)

        // "Ahora" = 10:00 hora local del negocio → se descartan 9:00 y 10:00.
        var slots = await CreateService().GetSlotsAsync(_businessId, _serviceId, _staffId, Date, nowUtc: Utc(10));

        Assert.Single(slots);
        Assert.Equal(Utc(11), slots[0].Start);
    }

    [Fact]
    public async Task GetSlots_ExcludesOccupied()
    {
        Setup(60, null, Open(Dow, 9, 12),
            reservations: new[] { new Reservation { StaffId = _staffId, Status = "pending", StartTime = Utc(10), EndTime = Utc(11) } });

        var slots = await CreateService().GetSlotsAsync(_businessId, _serviceId, _staffId, Date);

        Assert.Equal(2, slots.Count); // 9-10 y 11-12 (10-11 ocupado)
        Assert.DoesNotContain(slots, s => s.Start == Utc(10));
    }

    [Fact]
    public async Task GetSlots_ServiceNotInBusiness_Throws()
    {
        _services.Setup(s => s.GetByIdAsync(_serviceId, It.IsAny<CancellationToken>())).ReturnsAsync((Service?)null);
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = _businessId, OwnerId = Guid.NewGuid(), TierId = Guid.NewGuid(), Name = "Biz" });

        await Assert.ThrowsAsync<ServiceNotFoundException>(
            () => CreateService().GetSlotsAsync(_businessId, _serviceId, _staffId, Date));
    }
}
