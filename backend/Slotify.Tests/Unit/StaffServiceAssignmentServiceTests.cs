using Moq;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Asignación de servicios a un trabajador (qué puede realizar). Solo el owner;
/// valida que trabajador y servicios pertenecen al negocio.
/// </summary>
public class StaffServiceAssignmentServiceTests
{
    private readonly Mock<IStaffServiceRepository> _assignments = new();
    private readonly Mock<IStaffRepository> _staff = new();
    private readonly Mock<IServiceRepository> _services = new();
    private readonly Mock<IBusinessRepository> _businesses = new();

    private StaffServiceAssignmentService CreateService() =>
        new(_assignments.Object, _staff.Object, _services.Object, _businesses.Object);

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _staffId = Guid.NewGuid();

    private void OwnedBusiness() =>
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = _businessId, OwnerId = _ownerId, TierId = Guid.NewGuid(), Name = "Biz" });

    private void StaffInBusiness() =>
        _staff.Setup(s => s.GetByIdAsync(_staffId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Staff { Id = _staffId, BusinessId = _businessId, Name = "Ana", Role = "employee" });

    private void BusinessHasServices(params Guid[] ids) =>
        _services.Setup(s => s.ListByBusinessAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids.Select(id => new Service { Id = id, BusinessId = _businessId, Name = "S", DurationMinutes = 30 }).ToList());

    // ─── Set ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetAsync_AsOwnerWithValidServices_ReplacesAssignments()
    {
        var s1 = Guid.NewGuid();
        var s2 = Guid.NewGuid();
        OwnedBusiness();
        StaffInBusiness();
        BusinessHasServices(s1, s2);

        var result = await CreateService().SetAsync(_businessId, _staffId, _ownerId, new[] { s1, s2 });

        _assignments.Verify(a => a.SetForStaffAsync(_staffId,
            It.Is<IReadOnlyList<Guid>>(l => l.Count == 2 && l.Contains(s1) && l.Contains(s2)),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task SetAsync_WithEmptyList_ClearsAssignments_AndSkipsServiceLookup()
    {
        OwnedBusiness();
        StaffInBusiness();

        await CreateService().SetAsync(_businessId, _staffId, _ownerId, Array.Empty<Guid>());

        _assignments.Verify(a => a.SetForStaffAsync(_staffId,
            It.Is<IReadOnlyList<Guid>>(l => l.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
        _services.Verify(s => s.ListByBusinessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetAsync_WhenServiceNotInBusiness_Throws()
    {
        var valid = Guid.NewGuid();
        var alien = Guid.NewGuid();
        OwnedBusiness();
        StaffInBusiness();
        BusinessHasServices(valid);

        await Assert.ThrowsAsync<ServiceNotFoundException>(
            () => CreateService().SetAsync(_businessId, _staffId, _ownerId, new[] { valid, alien }));
        _assignments.Verify(a => a.SetForStaffAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetAsync_WhenNotOwner_Throws()
    {
        OwnedBusiness();

        await Assert.ThrowsAsync<NotBusinessOwnerException>(
            () => CreateService().SetAsync(_businessId, _staffId, Guid.NewGuid(), new[] { Guid.NewGuid() }));
        _assignments.Verify(a => a.SetForStaffAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetAsync_WhenStaffNotInBusiness_Throws()
    {
        OwnedBusiness();
        _staff.Setup(s => s.GetByIdAsync(_staffId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Staff { Id = _staffId, BusinessId = Guid.NewGuid(), Name = "Otro", Role = "employee" });

        await Assert.ThrowsAsync<StaffNotFoundException>(
            () => CreateService().SetAsync(_businessId, _staffId, _ownerId, new[] { Guid.NewGuid() }));
        _assignments.Verify(a => a.SetForStaffAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetAsync_WhenBusinessNotFound_Throws()
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync((Business?)null);

        await Assert.ThrowsAsync<BusinessNotFoundException>(
            () => CreateService().SetAsync(_businessId, _staffId, _ownerId, new[] { Guid.NewGuid() }));
    }

    // ─── List ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_AsOwner_ReturnsAssignedServiceIds()
    {
        var s1 = Guid.NewGuid();
        OwnedBusiness();
        StaffInBusiness();
        _assignments.Setup(a => a.ListServiceIdsByStaffAsync(_staffId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { s1 });

        var result = await CreateService().ListAsync(_businessId, _staffId, _ownerId);

        Assert.Single(result);
        Assert.Equal(s1, result[0]);
    }

    [Fact]
    public async Task ListAsync_WhenNotOwner_Throws()
    {
        OwnedBusiness();

        await Assert.ThrowsAsync<NotBusinessOwnerException>(
            () => CreateService().ListAsync(_businessId, _staffId, Guid.NewGuid()));
    }
}
