using Moq;
using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
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
}
