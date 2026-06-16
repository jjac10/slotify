using Moq;
using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Alta de servicios: solo el owner del negocio puede crear, y respetando el
/// límite Freemium del plan.
/// </summary>
public class ServiceServiceTests
{
    private readonly Mock<IServiceRepository> _services = new();
    private readonly Mock<IBusinessRepository> _businesses = new();
    private readonly Mock<IFreemiumLimitService> _limits = new();

    private ServiceService CreateService() => new(_services.Object, _businesses.Object, _limits.Object);

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _businessId = Guid.NewGuid();

    private Business OwnedBusiness() => new() { Id = _businessId, OwnerId = _ownerId, TierId = Guid.NewGuid(), Name = "Biz" };

    private static CreateServiceRequest Request() => new("Corte", "Corte clásico", 30, 15.00m, "#FF5733");

    [Fact]
    public async Task CreateAsync_AsOwnerUnderLimit_PersistsService()
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());
        _limits.Setup(l => l.CanAddServiceAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Service? saved = null;
        _services.Setup(s => s.AddAsync(It.IsAny<Service>(), It.IsAny<CancellationToken>()))
            .Callback<Service, CancellationToken>((s, _) => saved = s)
            .Returns(Task.CompletedTask);

        var result = await CreateService().CreateAsync(_businessId, _ownerId, Request());

        _services.Verify(s => s.AddAsync(It.IsAny<Service>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(saved);
        Assert.Equal(_businessId, saved!.BusinessId);
        Assert.Equal("Corte", saved.Name);
        Assert.Equal(30, saved.DurationMinutes);
        Assert.Equal(saved.Id, result.Id);
        Assert.Equal("Corte", result.Name);
    }

    [Fact]
    public async Task CreateAsync_WhenBusinessNotFound_Throws()
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync((Business?)null);

        await Assert.ThrowsAsync<BusinessNotFoundException>(
            () => CreateService().CreateAsync(_businessId, _ownerId, Request()));
        _services.Verify(s => s.AddAsync(It.IsAny<Service>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenNotOwner_Throws()
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());

        await Assert.ThrowsAsync<NotBusinessOwnerException>(
            () => CreateService().CreateAsync(_businessId, Guid.NewGuid(), Request()));
        _services.Verify(s => s.AddAsync(It.IsAny<Service>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenLimitReached_Throws()
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());
        _limits.Setup(l => l.CanAddServiceAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<FreemiumLimitReachedException>(
            () => CreateService().CreateAsync(_businessId, _ownerId, Request()));
        _services.Verify(s => s.AddAsync(It.IsAny<Service>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ListAsync_ReturnsServices()
    {
        _services.Setup(s => s.ListByBusinessAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Service>
            {
                new() { Id = Guid.NewGuid(), BusinessId = _businessId, Name = "Corte", DurationMinutes = 30 },
            });

        var result = await CreateService().ListAsync(_businessId);

        Assert.Single(result);
        Assert.Equal("Corte", result[0].Name);
    }
}
