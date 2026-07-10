using Moq;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Borrado de un negocio con "confirmación máxima" (owner: escribir el nombre exacto +
/// contraseña actual) y moderación del admin de plataforma (borra negocios spam sin
/// esas confirmaciones, incluso con reservas futuras). El borrado es en cascada
/// (servicios, staff, horarios, festivos, reservas, reseñas, invitados, notificaciones)
/// dentro de una transacción — datos personales fuera, por RGPD.
/// </summary>
public class BusinessDeletionServiceTests
{
    private readonly Mock<IAuthRepository> _auth = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IBusinessRepository> _businesses = new();
    private readonly Mock<IBusinessDeletionRepository> _deletion = new();

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _businessId = Guid.NewGuid();

    private BusinessDeletionService CreateService() =>
        new(_auth.Object, _hasher.Object, _businesses.Object, _deletion.Object);

    private void SetupBusiness(string name = "Barbería Elite")
        => _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = _businessId, OwnerId = _ownerId, TierId = Guid.NewGuid(), Name = name });

    private void SetupOwner(string passwordHash = "hash")
        => _auth.Setup(a => a.GetByIdAsync(_ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = _ownerId, Email = "pepe@test.local", Name = "Pepe", PasswordHash = passwordHash, Type = "owner" });

    private void PasswordOk(bool ok = true)
        => _hasher.Setup(h => h.Verify("SecurePass123!", "hash")).Returns(ok);

    // --- Owner: confirmación máxima ---

    [Fact]
    public async Task DeleteAsOwner_ExactNameAndPassword_DeletesCascade()
    {
        SetupBusiness();
        SetupOwner();
        PasswordOk();

        await CreateService().DeleteAsOwnerAsync(_businessId, _ownerId, "Barbería Elite", "SecurePass123!");

        _deletion.Verify(d => d.DeleteBusinessCascadeAsync(_businessId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsOwner_NameWithSurroundingSpaces_IsAccepted()
    {
        SetupBusiness();
        SetupOwner();
        PasswordOk();

        await CreateService().DeleteAsOwnerAsync(_businessId, _ownerId, "  Barbería Elite  ", "SecurePass123!");

        _deletion.Verify(d => d.DeleteBusinessCascadeAsync(_businessId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("barbería elite")] // distinto en mayúsculas: no vale (confirmación exacta)
    [InlineData("Barberia Elite")]
    [InlineData("")]
    [InlineData(null)]
    public async Task DeleteAsOwner_WrongName_ThrowsAndDoesNotDelete(string? confirmName)
    {
        SetupBusiness();
        SetupOwner();
        PasswordOk();

        await Assert.ThrowsAsync<BusinessNameMismatchException>(() =>
            CreateService().DeleteAsOwnerAsync(_businessId, _ownerId, confirmName, "SecurePass123!"));

        _deletion.Verify(d => d.DeleteBusinessCascadeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsOwner_WrongPassword_ThrowsAndDoesNotDelete()
    {
        SetupBusiness();
        SetupOwner();
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), "hash")).Returns(false);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            CreateService().DeleteAsOwnerAsync(_businessId, _ownerId, "Barbería Elite", "mala"));

        _deletion.Verify(d => d.DeleteBusinessCascadeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsOwner_NotTheOwner_Throws()
    {
        SetupBusiness();
        var intruderId = Guid.NewGuid();
        _auth.Setup(a => a.GetByIdAsync(intruderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = intruderId, Email = "otro@test.local", Name = "Otro", PasswordHash = "hash", Type = "owner" });
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), "hash")).Returns(true);

        await Assert.ThrowsAsync<NotBusinessOwnerException>(() =>
            CreateService().DeleteAsOwnerAsync(_businessId, intruderId, "Barbería Elite", "SecurePass123!"));
    }

    [Fact]
    public async Task DeleteAsOwner_WithFutureActiveReservations_Throws409()
    {
        SetupBusiness();
        SetupOwner();
        PasswordOk();
        _deletion.Setup(d => d.HasFutureActiveReservationsAsync(_businessId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<BusinessHasFutureReservationsException>(() =>
            CreateService().DeleteAsOwnerAsync(_businessId, _ownerId, "Barbería Elite", "SecurePass123!"));

        _deletion.Verify(d => d.DeleteBusinessCascadeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsOwner_BusinessNotFound_Throws()
    {
        SetupOwner();

        await Assert.ThrowsAsync<BusinessNotFoundException>(() =>
            CreateService().DeleteAsOwnerAsync(_businessId, _ownerId, "X", "SecurePass123!"));
    }

    // --- Admin: moderación ---

    [Fact]
    public async Task DeleteAsAdmin_DeletesCascade_EvenWithFutureReservations()
    {
        SetupBusiness();
        _deletion.Setup(d => d.HasFutureActiveReservationsAsync(_businessId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateService().DeleteAsAdminAsync(_businessId);

        _deletion.Verify(d => d.DeleteBusinessCascadeAsync(_businessId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsAdmin_BusinessNotFound_Throws()
    {
        await Assert.ThrowsAsync<BusinessNotFoundException>(() =>
            CreateService().DeleteAsAdminAsync(_businessId));
    }

    // --- Admin: directorio de negocios (moderación) ---

    [Fact]
    public async Task ListForAdmin_ReturnsPagedBusinessesWithOwnerEmail()
    {
        _deletion.Setup(d => d.ListForAdminAsync(null, 0, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(([
                (new Business { Id = _businessId, OwnerId = _ownerId, TierId = Guid.NewGuid(), Name = "Spam SL", CreatedAt = new DateTime(2026, 7, 1) }, "spam@test.local"),
            ], 7));

        var page = await CreateService().ListForAdminAsync(null);

        var dto = Assert.Single(page.Items);
        Assert.Equal("Spam SL", dto.Name);
        Assert.Equal("spam@test.local", dto.OwnerEmail);
        Assert.Equal(7, page.Total);
    }

    [Fact]
    public async Task ListForAdmin_InvalidPagination_Throws()
    {
        await Assert.ThrowsAsync<InvalidPaginationException>(() =>
            CreateService().ListForAdminAsync(null, page: 0));
    }
}

/// <summary>El rol de admin de plataforma sale de configuración (Admin:Email).</summary>
public class AdminOptionsTests
{
    [Fact]
    public void IsAdmin_MatchesConfiguredEmailIgnoringCase()
    {
        var options = new AdminOptions { Email = "admin@slotify.test" };

        Assert.True(options.IsAdmin("Admin@Slotify.test"));
        Assert.False(options.IsAdmin("otro@slotify.test"));
        Assert.False(options.IsAdmin(null));
    }

    [Fact]
    public void IsAdmin_WithoutConfiguredEmail_AlwaysFalse()
    {
        Assert.False(new AdminOptions().IsAdmin("admin@slotify.test"));
        Assert.False(new AdminOptions { Email = "  " }.IsAdmin("  "));
    }
}
