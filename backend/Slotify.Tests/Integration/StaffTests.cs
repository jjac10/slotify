using Microsoft.EntityFrameworkCore;
using Slotify.Domain.Entities;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// Capa de datos de `staff`. El owner ES un staff (role='owner'); un empleado
/// puede no tener cuenta (user_id NULL). Schema canónico: docs/DATA_MODEL.md (staff).
/// </summary>
public class StaffTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private SlotifyDbContext _db = null!;

    public StaffTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        _db = _fixture.CreateContext();
        await _db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task CreateStaff_AsOwner_Persists()
    {
        var (business, owner) = await SeedBusinessAsync();

        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            UserId = owner.Id,
            Role = "owner",
            Name = "Owner Test",
        };
        _db.Staff.Add(staff);
        await _db.SaveChangesAsync();

        var loaded = await _db.Staff.AsNoTracking().SingleAsync(s => s.Id == staff.Id);
        Assert.Equal("owner", loaded.Role);
        Assert.Equal(owner.Id, loaded.UserId);
        Assert.Equal(business.Id, loaded.BusinessId);
    }

    [Fact]
    public async Task CreateStaff_EmployeeWithoutUser_Persists()
    {
        var (business, _) = await SeedBusinessAsync();

        // Empleado sin login: user_id es NULL.
        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            UserId = null,
            Role = "employee",
            Name = "Empleado sin cuenta",
        };
        _db.Staff.Add(staff);
        await _db.SaveChangesAsync();

        var loaded = await _db.Staff.AsNoTracking().SingleAsync(s => s.Id == staff.Id);
        Assert.Null(loaded.UserId);
        Assert.Equal("employee", loaded.Role);
    }

    [Fact]
    public async Task CreateStaff_WithoutValidBusiness_IsRejected()
    {
        // business_id es NOT NULL + FK: un negocio inexistente debe romper el guardado.
        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            BusinessId = Guid.NewGuid(), // no existe
            Role = "employee",
            Name = "Huérfano",
        };
        _db.Staff.Add(staff);

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    private async Task<(Business business, User owner)> SeedBusinessAsync()
    {
        var owner = new User
        {
            Id = Guid.NewGuid(),
            Email = $"owner-{Guid.NewGuid():N}@test.local",
            PasswordHash = "hash",
            Name = "Owner Test",
            Type = "owner",
        };
        var free = await _db.PricingTiers.AsNoTracking().SingleAsync(t => t.Code == "free");
        var business = new Business
        {
            Id = Guid.NewGuid(),
            OwnerId = owner.Id,
            TierId = free.Id,
            Name = "Negocio Test",
        };
        _db.Users.Add(owner);
        _db.Businesses.Add(business);
        await _db.SaveChangesAsync();
        return (business, owner);
    }
}
