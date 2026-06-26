using Moq;
using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Owner-as-staff al crear el negocio + configuración del owner (modo de
/// confirmación, ventana de cancelación, plan/tier). Repos mockeados (ADR #2).
/// </summary>
public class BusinessServiceTests
{
    private readonly Mock<IBusinessRepository> _repo = new();
    private readonly Mock<ITierRepository> _tiers = new();

    private BusinessService CreateService() => new(_repo.Object, _tiers.Object);

    [Fact]
    public async Task CreateAsync_BuildsBusinessWithOwnerStaff_AndPersistsOnce()
    {
        Business? savedBusiness = null;
        Staff? savedStaff = null;
        _repo.Setup(r => r.AddWithOwnerStaffAsync(
                It.IsAny<Business>(), It.IsAny<Staff>(), It.IsAny<CancellationToken>()))
            .Callback<Business, Staff, CancellationToken>((b, s, _) => { savedBusiness = b; savedStaff = s; })
            .Returns(Task.CompletedTask);

        var ownerId = Guid.NewGuid();
        var tierId = Guid.NewGuid();
        var request = new CreateBusinessRequest(ownerId, tierId, "Barbería Pepe", "Pepe");

        var result = await CreateService().CreateAsync(request);

        _repo.Verify(r => r.AddWithOwnerStaffAsync(
            It.IsAny<Business>(), It.IsAny<Staff>(), It.IsAny<CancellationToken>()), Times.Once);

        Assert.NotNull(savedBusiness);
        Assert.Equal(ownerId, savedBusiness!.OwnerId);
        Assert.Equal(tierId, savedBusiness.TierId);
        Assert.Equal("Barbería Pepe", savedBusiness.Name);

        Assert.NotNull(savedStaff);
        Assert.Equal("owner", savedStaff!.Role);
        Assert.Equal(ownerId, savedStaff.UserId);
        Assert.Equal(savedBusiness.Id, savedStaff.BusinessId); // owner-staff ligado al negocio
        Assert.Equal("Pepe", savedStaff.Name);

        Assert.Equal(savedBusiness.Id, result.Id);
    }

    // --- Modo de confirmación -----------------------------------------------

    [Theory]
    [InlineData("auto")]
    [InlineData("manual")]
    public async Task SetConfirmationModeAsync_AsOwner_UpdatesMode(string mode)
    {
        var ownerId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var business = new Business { Id = businessId, OwnerId = ownerId, TierId = Guid.NewGuid(), Name = "Biz", ConfirmationMode = "auto" };
        _repo.Setup(r => r.GetByIdAsync(businessId, It.IsAny<CancellationToken>())).ReturnsAsync(business);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateService().SetConfirmationModeAsync(businessId, ownerId, mode);

        Assert.Equal(mode, business.ConfirmationMode);
        Assert.Equal(mode, result.ConfirmationMode);
        _repo.Verify(r => r.UpdateAsync(business, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetConfirmationModeAsync_InvalidMode_Throws_AndDoesNotUpdate()
    {
        await Assert.ThrowsAsync<InvalidConfirmationModeException>(
            () => CreateService().SetConfirmationModeAsync(Guid.NewGuid(), Guid.NewGuid(), "nope"));

        _repo.Verify(r => r.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetConfirmationModeAsync_NotOwner_Throws_AndDoesNotUpdate()
    {
        var businessId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = businessId, OwnerId = Guid.NewGuid(), TierId = Guid.NewGuid(), Name = "Biz" });

        await Assert.ThrowsAsync<NotBusinessOwnerException>(
            () => CreateService().SetConfirmationModeAsync(businessId, Guid.NewGuid(), "manual"));

        _repo.Verify(r => r.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("online")]
    [InlineData("calendar_only")]
    public async Task SetBookingModeAsync_AsOwner_UpdatesMode(string mode)
    {
        var ownerId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var business = new Business { Id = businessId, OwnerId = ownerId, TierId = Guid.NewGuid(), Name = "Biz", BookingMode = "online" };
        _repo.Setup(r => r.GetByIdAsync(businessId, It.IsAny<CancellationToken>())).ReturnsAsync(business);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateService().SetBookingModeAsync(businessId, ownerId, mode);

        Assert.Equal(mode, business.BookingMode);
        Assert.Equal(mode, result.BookingMode);
        _repo.Verify(r => r.UpdateAsync(business, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetBookingModeAsync_InvalidMode_Throws_AndDoesNotUpdate()
    {
        await Assert.ThrowsAsync<InvalidBookingModeException>(
            () => CreateService().SetBookingModeAsync(Guid.NewGuid(), Guid.NewGuid(), "nope"));

        _repo.Verify(r => r.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetBookingModeAsync_NotOwner_Throws_AndDoesNotUpdate()
    {
        var businessId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = businessId, OwnerId = Guid.NewGuid(), TierId = Guid.NewGuid(), Name = "Biz" });

        await Assert.ThrowsAsync<NotBusinessOwnerException>(
            () => CreateService().SetBookingModeAsync(businessId, Guid.NewGuid(), "calendar_only"));

        _repo.Verify(r => r.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Ventana de cancelación ---------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(24)]
    [InlineData(720)]
    public async Task SetCancellationCutoffAsync_AsOwner_UpdatesHours(int hours)
    {
        var ownerId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var business = new Business { Id = businessId, OwnerId = ownerId, TierId = Guid.NewGuid(), Name = "Biz" };
        _repo.Setup(r => r.GetByIdAsync(businessId, It.IsAny<CancellationToken>())).ReturnsAsync(business);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateService().SetCancellationCutoffAsync(businessId, ownerId, hours);

        Assert.Equal(hours, business.CancellationCutoffHours);
        Assert.Equal(hours, result.CancellationCutoffHours);
        _repo.Verify(r => r.UpdateAsync(business, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(721)]
    public async Task SetCancellationCutoffAsync_OutOfRange_Throws_AndDoesNotUpdate(int hours)
    {
        await Assert.ThrowsAsync<InvalidCancellationCutoffException>(
            () => CreateService().SetCancellationCutoffAsync(Guid.NewGuid(), Guid.NewGuid(), hours));

        _repo.Verify(r => r.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetCancellationCutoffAsync_NotOwner_Throws_AndDoesNotUpdate()
    {
        var businessId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = businessId, OwnerId = Guid.NewGuid(), TierId = Guid.NewGuid(), Name = "Biz" });

        await Assert.ThrowsAsync<NotBusinessOwnerException>(
            () => CreateService().SetCancellationCutoffAsync(businessId, Guid.NewGuid(), 24));

        _repo.Verify(r => r.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Plan / tier --------------------------------------------------------

    [Theory]
    [InlineData("premium")]
    [InlineData("free")]
    public async Task ChangePlanAsync_AsOwner_SetsTier_AndReturnsPlan(string code)
    {
        var ownerId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var newTierId = Guid.NewGuid();
        var business = new Business { Id = businessId, OwnerId = ownerId, TierId = Guid.NewGuid(), Name = "Biz" };
        _repo.Setup(r => r.GetByIdAsync(businessId, It.IsAny<CancellationToken>())).ReturnsAsync(business);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _tiers.Setup(t => t.GetByCodeAsync(code, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingTier { Id = newTierId, Code = code, Name = code });

        var result = await CreateService().ChangePlanAsync(businessId, ownerId, code);

        Assert.Equal(newTierId, business.TierId);
        Assert.Equal(code, result.Plan);
        _repo.Verify(r => r.UpdateAsync(business, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePlanAsync_InvalidCode_Throws_AndDoesNotUpdate()
    {
        await Assert.ThrowsAsync<InvalidPlanException>(
            () => CreateService().ChangePlanAsync(Guid.NewGuid(), Guid.NewGuid(), "gold"));

        _repo.Verify(r => r.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>()), Times.Never);
        _tiers.Verify(t => t.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangePlanAsync_NotOwner_Throws_AndDoesNotUpdate()
    {
        var businessId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = businessId, OwnerId = Guid.NewGuid(), TierId = Guid.NewGuid(), Name = "Biz" });

        await Assert.ThrowsAsync<NotBusinessOwnerException>(
            () => CreateService().ChangePlanAsync(businessId, Guid.NewGuid(), "premium"));

        _repo.Verify(r => r.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangePlanAsync_BusinessNotFound_Throws()
    {
        var businessId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(businessId, It.IsAny<CancellationToken>())).ReturnsAsync((Business?)null);

        await Assert.ThrowsAsync<BusinessNotFoundException>(
            () => CreateService().ChangePlanAsync(businessId, Guid.NewGuid(), "premium"));

        _repo.Verify(r => r.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Perfil público -----------------------------------------------------

    [Fact]
    public async Task UpdateProfileAsync_AsOwner_UpdatesFields()
    {
        var ownerId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        var business = new Business { Id = businessId, OwnerId = ownerId, TierId = Guid.NewGuid(), Name = "Biz" };
        _repo.Setup(r => r.GetByIdAsync(businessId, It.IsAny<CancellationToken>())).ReturnsAsync(business);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateService().UpdateProfileAsync(businessId, ownerId,
            new UpdateBusinessProfileRequest("barberia", "https://img/x.jpg", 40.4, -3.7));

        Assert.Equal("barberia", business.Category);
        Assert.Equal("https://img/x.jpg", business.PhotoUrl);
        Assert.Equal(40.4, business.Latitude);
        Assert.Equal(-3.7, business.Longitude);
        Assert.Equal("barberia", result.Category);
        _repo.Verify(r => r.UpdateAsync(business, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProfileAsync_InvalidCategory_Throws_AndDoesNotUpdate()
    {
        await Assert.ThrowsAsync<InvalidCategoryException>(() => CreateService().UpdateProfileAsync(
            Guid.NewGuid(), Guid.NewGuid(), new UpdateBusinessProfileRequest("no-existe", null, null, null)));

        _repo.Verify(r => r.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProfileAsync_NotOwner_Throws_AndDoesNotUpdate()
    {
        var businessId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = businessId, OwnerId = Guid.NewGuid(), TierId = Guid.NewGuid(), Name = "Biz" });

        await Assert.ThrowsAsync<NotBusinessOwnerException>(() => CreateService().UpdateProfileAsync(
            businessId, Guid.NewGuid(), new UpdateBusinessProfileRequest("spa", null, null, null)));

        _repo.Verify(r => r.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
