using Moq;
using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Gestión del horario del negocio: solo el owner puede fijar horario/festivos,
/// con validación (día 0–6, apertura&lt;cierre, sin días duplicados).
/// </summary>
public class BusinessScheduleServiceTests
{
    private readonly Mock<IBusinessRepository> _businesses = new();
    private readonly Mock<IBusinessHourRepository> _hours = new();
    private readonly Mock<IBusinessHolidayRepository> _holidays = new();

    private BusinessScheduleService CreateService() => new(_businesses.Object, _hours.Object, _holidays.Object);

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _businessId = Guid.NewGuid();

    private Business OwnedBusiness() => new() { Id = _businessId, OwnerId = _ownerId, TierId = Guid.NewGuid(), Name = "Biz" };

    private static BusinessHourInput Open(int day) => new(day, false, new TimeOnly(9, 0), new TimeOnly(18, 0));

    [Fact]
    public async Task SetHoursAsync_AsOwner_ReplacesHours()
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());
        IEnumerable<BusinessHour>? saved = null;
        _hours.Setup(h => h.ReplaceForBusinessAsync(_businessId, It.IsAny<IEnumerable<BusinessHour>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, IEnumerable<BusinessHour>, CancellationToken>((_, hs, _) => saved = hs)
            .Returns(Task.CompletedTask);

        await CreateService().SetHoursAsync(_businessId, _ownerId, new[] { Open(1), new BusinessHourInput(0, true, null, null) });

        _hours.Verify(h => h.ReplaceForBusinessAsync(_businessId, It.IsAny<IEnumerable<BusinessHour>>(), It.IsAny<CancellationToken>()), Times.Once);
        var list = saved!.ToList();
        Assert.Equal(2, list.Count);
        Assert.Contains(list, h => h.DayOfWeek == 1 && h.OpeningTime == new TimeOnly(9, 0));
        Assert.Contains(list, h => h.DayOfWeek == 0 && h.IsClosed);
    }

    [Fact]
    public async Task SetHoursAsync_NotOwner_Throws()
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());

        await Assert.ThrowsAsync<NotBusinessOwnerException>(
            () => CreateService().SetHoursAsync(_businessId, Guid.NewGuid(), new[] { Open(1) }));
        _hours.Verify(h => h.ReplaceForBusinessAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<BusinessHour>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(-1)]
    public async Task SetHoursAsync_InvalidDay_Throws(int badDay)
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());

        await Assert.ThrowsAsync<InvalidBusinessHoursException>(
            () => CreateService().SetHoursAsync(_businessId, _ownerId, new[] { Open(badDay) }));
    }

    [Fact]
    public async Task SetHoursAsync_OpeningAfterClosing_Throws()
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());

        await Assert.ThrowsAsync<InvalidBusinessHoursException>(
            () => CreateService().SetHoursAsync(_businessId, _ownerId, new[] { new BusinessHourInput(1, false, new TimeOnly(18, 0), new TimeOnly(9, 0)) }));
    }

    [Fact]
    public async Task SetHoursAsync_DuplicateDay_Throws()
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());

        await Assert.ThrowsAsync<InvalidBusinessHoursException>(
            () => CreateService().SetHoursAsync(_businessId, _ownerId, new[] { Open(1), Open(1) }));
    }

    [Fact]
    public async Task AddHolidayAsync_AsOwner_Adds()
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());
        BusinessHoliday? saved = null;
        _holidays.Setup(h => h.AddAsync(It.IsAny<BusinessHoliday>(), It.IsAny<CancellationToken>()))
            .Callback<BusinessHoliday, CancellationToken>((h, _) => saved = h).Returns(Task.CompletedTask);

        var result = await CreateService().AddHolidayAsync(_businessId, _ownerId,
            new CreateHolidayRequest(new DateOnly(2026, 12, 25), "Navidad", true));

        Assert.NotNull(saved);
        Assert.Equal(new DateOnly(2026, 12, 25), saved!.HolidayDate);
        Assert.Equal(_businessId, saved.BusinessId);
        Assert.Equal(saved.Id, result.Id);
    }

    [Fact]
    public async Task AddHolidayAsync_RangeAndHours_Persisted()
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());
        BusinessHoliday? saved = null;
        _holidays.Setup(h => h.AddAsync(It.IsAny<BusinessHoliday>(), It.IsAny<CancellationToken>()))
            .Callback<BusinessHoliday, CancellationToken>((h, _) => saved = h).Returns(Task.CompletedTask);

        var result = await CreateService().AddHolidayAsync(_businessId, _ownerId,
            new CreateHolidayRequest(new DateOnly(2026, 8, 1), "Vacaciones", true,
                EndDate: new DateOnly(2026, 8, 15), StartTime: new TimeOnly(14, 0), EndTime: new TimeOnly(16, 0)));

        Assert.Equal(new DateOnly(2026, 8, 15), saved!.EndDate);
        Assert.Equal(new TimeOnly(14, 0), saved.StartTime);
        Assert.Equal(new TimeOnly(16, 0), saved.EndTime);
        Assert.Equal(new DateOnly(2026, 8, 15), result.EndDate);
    }

    [Fact]
    public async Task AddHolidayAsync_EndDateBeforeStart_Throws()
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());

        await Assert.ThrowsAsync<InvalidHolidayException>(() => CreateService().AddHolidayAsync(_businessId, _ownerId,
            new CreateHolidayRequest(new DateOnly(2026, 8, 10), null, true, EndDate: new DateOnly(2026, 8, 1))));
        _holidays.Verify(h => h.AddAsync(It.IsAny<BusinessHoliday>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddHolidayAsync_OnlyStartTime_Throws()
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());

        await Assert.ThrowsAsync<InvalidHolidayException>(() => CreateService().AddHolidayAsync(_businessId, _ownerId,
            new CreateHolidayRequest(new DateOnly(2026, 8, 10), null, true, StartTime: new TimeOnly(14, 0))));
    }

    [Fact]
    public async Task AddHolidayAsync_StartAfterEndTime_Throws()
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());

        await Assert.ThrowsAsync<InvalidHolidayException>(() => CreateService().AddHolidayAsync(_businessId, _ownerId,
            new CreateHolidayRequest(new DateOnly(2026, 8, 10), null, true, StartTime: new TimeOnly(16, 0), EndTime: new TimeOnly(14, 0))));
    }

    [Fact]
    public async Task DeleteHolidayAsync_NotOwner_Throws()
    {
        var holiday = new BusinessHoliday { Id = Guid.NewGuid(), BusinessId = _businessId, HolidayDate = new DateOnly(2026, 1, 1) };
        _holidays.Setup(h => h.GetByIdAsync(holiday.Id, It.IsAny<CancellationToken>())).ReturnsAsync(holiday);
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());

        await Assert.ThrowsAsync<NotBusinessOwnerException>(
            () => CreateService().DeleteHolidayAsync(holiday.Id, Guid.NewGuid()));
        _holidays.Verify(h => h.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
