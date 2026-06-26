using Moq;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Validación de límites Freemium data-driven (ADR #9): el límite se lee del
/// tier del negocio (businesses.tier_id). NULL en un límite = ilimitado.
/// </summary>
public class FreemiumLimitServiceTests
{
    private readonly Mock<ITierRepository> _tiers = new();
    private readonly Mock<IStaffRepository> _staff = new();
    private readonly Mock<IServiceRepository> _services = new();
    private readonly Mock<IReservationRepository> _reservations = new();

    private FreemiumLimitService CreateService() =>
        new(_tiers.Object, _staff.Object, _services.Object, _reservations.Object);

    private static readonly DateTime NowUtc = new(2026, 6, 17, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CanAddStaff_FreeAtLimit_ReturnsFalse()
    {
        var businessId = Guid.NewGuid();
        _tiers.Setup(t => t.GetByBusinessAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingTier { Code = "free", MaxStaff = 1 });
        _staff.Setup(s => s.CountByBusinessAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1); // ya tiene el owner

        var result = await CreateService().CanAddStaffAsync(businessId);

        Assert.False(result);
    }

    [Fact]
    public async Task CanAddStaff_FreeUnderLimit_ReturnsTrue()
    {
        var businessId = Guid.NewGuid();
        _tiers.Setup(t => t.GetByBusinessAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingTier { Code = "free", MaxStaff = 1 });
        _staff.Setup(s => s.CountByBusinessAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await CreateService().CanAddStaffAsync(businessId);

        Assert.True(result);
    }

    [Fact]
    public async Task CanAddStaff_PremiumUnlimited_ReturnsTrue_WithoutCounting()
    {
        var businessId = Guid.NewGuid();
        _tiers.Setup(t => t.GetByBusinessAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingTier { Code = "premium", MaxStaff = null }); // ilimitado

        var result = await CreateService().CanAddStaffAsync(businessId);

        Assert.True(result);
        // Si es ilimitado no hace falta contar: evita una query innecesaria.
        _staff.Verify(s => s.CountByBusinessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CanAddService_FreeAtLimit_ReturnsFalse()
    {
        var businessId = Guid.NewGuid();
        _tiers.Setup(t => t.GetByBusinessAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingTier { Code = "free", MaxServices = 5 });
        _services.Setup(s => s.CountByBusinessAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        Assert.False(await CreateService().CanAddServiceAsync(businessId));
    }

    [Fact]
    public async Task CanAddService_FreeUnderLimit_ReturnsTrue()
    {
        var businessId = Guid.NewGuid();
        _tiers.Setup(t => t.GetByBusinessAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingTier { Code = "free", MaxServices = 5 });
        _services.Setup(s => s.CountByBusinessAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        Assert.True(await CreateService().CanAddServiceAsync(businessId));
    }

    [Fact]
    public async Task CanAddService_PremiumUnlimited_ReturnsTrue_WithoutCounting()
    {
        var businessId = Guid.NewGuid();
        _tiers.Setup(t => t.GetByBusinessAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingTier { Code = "premium", MaxServices = null });

        Assert.True(await CreateService().CanAddServiceAsync(businessId));
        _services.Verify(s => s.CountByBusinessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CanAddReservationThisMonth_FreeUnderLimit_ReturnsTrue()
    {
        var businessId = Guid.NewGuid();
        _tiers.Setup(t => t.GetByBusinessAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingTier { Code = "free", MaxReservationsPerMonth = 100 });
        _reservations.Setup(r => r.CountByBusinessAsync(businessId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(99);

        Assert.True(await CreateService().CanAddReservationThisMonthAsync(businessId, NowUtc));
    }

    [Fact]
    public async Task CanAddReservationThisMonth_FreeAtLimit_ReturnsFalse()
    {
        var businessId = Guid.NewGuid();
        _tiers.Setup(t => t.GetByBusinessAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingTier { Code = "free", MaxReservationsPerMonth = 100 });
        _reservations.Setup(r => r.CountByBusinessAsync(businessId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100); // ya tiene 100 este mes → la 101 se bloquea

        Assert.False(await CreateService().CanAddReservationThisMonthAsync(businessId, NowUtc));
    }

    [Fact]
    public async Task CanAddReservationThisMonth_FreeOverLimit_ReturnsFalse()
    {
        var businessId = Guid.NewGuid();
        _tiers.Setup(t => t.GetByBusinessAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingTier { Code = "free", MaxReservationsPerMonth = 100 });
        _reservations.Setup(r => r.CountByBusinessAsync(businessId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(150);

        Assert.False(await CreateService().CanAddReservationThisMonthAsync(businessId, NowUtc));
    }

    [Fact]
    public async Task CanAddReservationThisMonth_PremiumUnlimited_ReturnsTrue_WithoutCounting()
    {
        var businessId = Guid.NewGuid();
        _tiers.Setup(t => t.GetByBusinessAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingTier { Code = "premium", MaxReservationsPerMonth = null });

        Assert.True(await CreateService().CanAddReservationThisMonthAsync(businessId, NowUtc));
        // Ilimitado: no debe contar (evita query innecesaria).
        _reservations.Verify(r => r.CountByBusinessAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CanAddReservationThisMonth_CountsOnlyCurrentMonthWindow()
    {
        var businessId = Guid.NewGuid();
        _tiers.Setup(t => t.GetByBusinessAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingTier { Code = "free", MaxReservationsPerMonth = 100 });
        DateTime? capturedFrom = null, capturedTo = null;
        _reservations.Setup(r => r.CountByBusinessAsync(businessId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, DateTime?, DateTime?, CancellationToken>((_, from, to, _) => { capturedFrom = from; capturedTo = to; })
            .ReturnsAsync(0);

        await CreateService().CanAddReservationThisMonthAsync(businessId, NowUtc);

        // Ventana [primer día del mes 00:00 UTC, primer día del mes siguiente 00:00 UTC).
        Assert.Equal(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), capturedFrom);
        Assert.Equal(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), capturedTo);
    }
}
