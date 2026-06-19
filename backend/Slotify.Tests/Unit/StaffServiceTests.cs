using Moq;
using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Gestión de trabajadores: listado público + alta/edición/baja por el owner
/// (con límite Freemium y protección del owner-staff).
/// </summary>
public class StaffServiceTests
{
    private readonly Mock<IStaffRepository> _staff = new();
    private readonly Mock<IBusinessRepository> _businesses = new();
    private readonly Mock<IFreemiumLimitService> _limits = new();

    private StaffService CreateService() => new(_staff.Object, _businesses.Object, _limits.Object);

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _businessId = Guid.NewGuid();

    private Business OwnedBusiness() => new() { Id = _businessId, OwnerId = _ownerId, TierId = Guid.NewGuid(), Name = "Biz" };

    private static CreateStaffRequest CreateRequest() => new("Ana", "ana@test.local", "+34600111222");

    // ─── Listado ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_MapsStaffToResponses()
    {
        var staffId = Guid.NewGuid();
        _staff.Setup(s => s.ListByBusinessAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Staff>
            {
                new() { Id = staffId, BusinessId = _businessId, Name = "Pepe", Role = "owner", Status = "active" },
            });

        var result = await CreateService().ListAsync(_businessId);

        Assert.Single(result);
        Assert.Equal(staffId, result[0].Id);
        Assert.Equal(_businessId, result[0].BusinessId);
        Assert.Equal("Pepe", result[0].Name);
        Assert.Equal("owner", result[0].Role);
        Assert.Equal("active", result[0].Status);
    }

    [Fact]
    public async Task ListAsync_WhenNoStaff_ReturnsEmpty()
    {
        _staff.Setup(s => s.ListByBusinessAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Staff>());

        var result = await CreateService().ListAsync(_businessId);

        Assert.Empty(result);
    }

    // ─── Alta ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_AsOwnerUnderLimit_PersistsEmployee()
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());
        _limits.Setup(l => l.CanAddStaffAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Staff? saved = null;
        _staff.Setup(s => s.AddAsync(It.IsAny<Staff>(), It.IsAny<CancellationToken>()))
            .Callback<Staff, CancellationToken>((s, _) => saved = s)
            .Returns(Task.CompletedTask);

        var result = await CreateService().CreateAsync(_businessId, _ownerId, CreateRequest());

        _staff.Verify(s => s.AddAsync(It.IsAny<Staff>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(saved);
        Assert.Equal(_businessId, saved!.BusinessId);
        Assert.Equal("Ana", saved.Name);
        Assert.Equal("employee", saved.Role);
        Assert.Equal("active", saved.Status);
        Assert.Equal("ana@test.local", saved.Email);
        Assert.Equal("+34600111222", saved.Phone);
        Assert.Equal(saved.Id, result.Id);
        Assert.Equal("Ana", result.Name);
    }

    [Fact]
    public async Task CreateAsync_WhenBusinessNotFound_Throws()
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync((Business?)null);

        await Assert.ThrowsAsync<BusinessNotFoundException>(
            () => CreateService().CreateAsync(_businessId, _ownerId, CreateRequest()));
        _staff.Verify(s => s.AddAsync(It.IsAny<Staff>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenNotOwner_Throws()
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());

        await Assert.ThrowsAsync<NotBusinessOwnerException>(
            () => CreateService().CreateAsync(_businessId, Guid.NewGuid(), CreateRequest()));
        _staff.Verify(s => s.AddAsync(It.IsAny<Staff>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenLimitReached_Throws()
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());
        _limits.Setup(l => l.CanAddStaffAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Assert.ThrowsAsync<FreemiumLimitReachedException>(
            () => CreateService().CreateAsync(_businessId, _ownerId, CreateRequest()));
        _staff.Verify(s => s.AddAsync(It.IsAny<Staff>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── Edición ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_AsOwner_UpdatesEmployee()
    {
        var staffId = Guid.NewGuid();
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());
        _staff.Setup(s => s.GetByIdAsync(staffId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Staff { Id = staffId, BusinessId = _businessId, Name = "Ana", Role = "employee", Status = "active" });

        var result = await CreateService().UpdateAsync(_businessId, staffId, _ownerId, new UpdateStaffRequest("Ana María", "am@test.local", "+34600999888"));

        _staff.Verify(s => s.UpdateAsync(It.Is<Staff>(x => x.Id == staffId && x.Name == "Ana María" && x.Email == "am@test.local" && x.Phone == "+34600999888"), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("Ana María", result.Name);
    }

    [Fact]
    public async Task UpdateAsync_WhenNotOwner_Throws()
    {
        var staffId = Guid.NewGuid();
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());

        await Assert.ThrowsAsync<NotBusinessOwnerException>(
            () => CreateService().UpdateAsync(_businessId, staffId, Guid.NewGuid(), new UpdateStaffRequest("X", null, null)));
        _staff.Verify(s => s.UpdateAsync(It.IsAny<Staff>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenStaffNotInBusiness_Throws()
    {
        var staffId = Guid.NewGuid();
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());
        _staff.Setup(s => s.GetByIdAsync(staffId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Staff { Id = staffId, BusinessId = Guid.NewGuid(), Name = "Otro", Role = "employee" });

        await Assert.ThrowsAsync<StaffNotFoundException>(
            () => CreateService().UpdateAsync(_businessId, staffId, _ownerId, new UpdateStaffRequest("X", null, null)));
        _staff.Verify(s => s.UpdateAsync(It.IsAny<Staff>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ─── Baja (soft delete) ─────────────────────────────────────────────────────

    [Fact]
    public async Task DeactivateAsync_AsOwner_MarksInactive()
    {
        var staffId = Guid.NewGuid();
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());
        _staff.Setup(s => s.GetByIdAsync(staffId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Staff { Id = staffId, BusinessId = _businessId, Name = "Ana", Role = "employee", Status = "active" });

        await CreateService().DeactivateAsync(_businessId, staffId, _ownerId);

        _staff.Verify(s => s.UpdateAsync(It.Is<Staff>(x => x.Id == staffId && x.Status == "inactive"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeactivateAsync_WhenStaffIsOwner_Throws()
    {
        var staffId = Guid.NewGuid();
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());
        _staff.Setup(s => s.GetByIdAsync(staffId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Staff { Id = staffId, BusinessId = _businessId, Name = "Pepe", Role = "owner", Status = "active" });

        await Assert.ThrowsAsync<CannotModifyOwnerStaffException>(
            () => CreateService().DeactivateAsync(_businessId, staffId, _ownerId));
        _staff.Verify(s => s.UpdateAsync(It.IsAny<Staff>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeactivateAsync_WhenNotOwner_Throws()
    {
        var staffId = Guid.NewGuid();
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(OwnedBusiness());

        await Assert.ThrowsAsync<NotBusinessOwnerException>(
            () => CreateService().DeactivateAsync(_businessId, staffId, Guid.NewGuid()));
        _staff.Verify(s => s.UpdateAsync(It.IsAny<Staff>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
