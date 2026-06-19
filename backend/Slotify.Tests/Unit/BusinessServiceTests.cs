using Moq;
using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Owner-as-staff: al crear un negocio, el servicio crea también su Staff
/// role='owner' enlazado al owner. Test unitario con repositorio mockeado (ADR #2).
/// </summary>
public class BusinessServiceTests
{
    [Fact]
    public async Task CreateAsync_BuildsBusinessWithOwnerStaff_AndPersistsOnce()
    {
        var repo = new Mock<IBusinessRepository>();
        Business? savedBusiness = null;
        Staff? savedStaff = null;
        repo.Setup(r => r.AddWithOwnerStaffAsync(
                It.IsAny<Business>(), It.IsAny<Staff>(), It.IsAny<CancellationToken>()))
            .Callback<Business, Staff, CancellationToken>((b, s, _) => { savedBusiness = b; savedStaff = s; })
            .Returns(Task.CompletedTask);

        var service = new BusinessService(repo.Object);
        var ownerId = Guid.NewGuid();
        var tierId = Guid.NewGuid();
        var request = new CreateBusinessRequest(ownerId, tierId, "Barbería Pepe", "Pepe");

        var result = await service.CreateAsync(request);

        repo.Verify(r => r.AddWithOwnerStaffAsync(
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
        var repo = new Mock<IBusinessRepository>();
        var business = new Business { Id = businessId, OwnerId = ownerId, TierId = Guid.NewGuid(), Name = "Biz", ConfirmationMode = "auto" };
        repo.Setup(r => r.GetByIdAsync(businessId, It.IsAny<CancellationToken>())).ReturnsAsync(business);
        repo.Setup(r => r.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await new BusinessService(repo.Object).SetConfirmationModeAsync(businessId, ownerId, mode);

        Assert.Equal(mode, business.ConfirmationMode);
        Assert.Equal(mode, result.ConfirmationMode);
        repo.Verify(r => r.UpdateAsync(business, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetConfirmationModeAsync_InvalidMode_Throws_AndDoesNotUpdate()
    {
        var repo = new Mock<IBusinessRepository>();

        await Assert.ThrowsAsync<InvalidConfirmationModeException>(
            () => new BusinessService(repo.Object).SetConfirmationModeAsync(Guid.NewGuid(), Guid.NewGuid(), "nope"));

        repo.Verify(r => r.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetConfirmationModeAsync_NotOwner_Throws_AndDoesNotUpdate()
    {
        var businessId = Guid.NewGuid();
        var repo = new Mock<IBusinessRepository>();
        repo.Setup(r => r.GetByIdAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = businessId, OwnerId = Guid.NewGuid(), TierId = Guid.NewGuid(), Name = "Biz" });

        await Assert.ThrowsAsync<NotBusinessOwnerException>(
            () => new BusinessService(repo.Object).SetConfirmationModeAsync(businessId, Guid.NewGuid(), "manual"));

        repo.Verify(r => r.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
