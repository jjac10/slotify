using Moq;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>Listado público de trabajadores de un negocio (para elegir con quién reservar).</summary>
public class StaffServiceTests
{
    private readonly Mock<IStaffRepository> _staff = new();

    private StaffService CreateService() => new(_staff.Object);

    private readonly Guid _businessId = Guid.NewGuid();

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
}
