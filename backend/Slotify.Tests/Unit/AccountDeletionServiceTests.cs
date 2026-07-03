using Moq;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Borrado de cuenta (derecho de supresión RGPD): exige la contraseña actual,
/// anonimiza el user (tombstone sin datos personales) y delega el borrado
/// transaccional en el repositorio. Un owner con reservas futuras no puede borrar.
/// </summary>
public class AccountDeletionServiceTests
{
    private readonly Mock<IAuthRepository> _auth = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IBusinessRepository> _businesses = new();
    private readonly Mock<IAccountDeletionRepository> _deletion = new();

    private AccountDeletionService CreateService() =>
        new(_auth.Object, _hasher.Object, _businesses.Object, _deletion.Object);

    private User SetupUser(string type)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "ana@example.com",
            PasswordHash = "hashed-pw",
            Name = "Ana",
            Phone = "+34600000000",
            Type = type,
            EmailVerifiedAt = DateTime.UtcNow,
        };
        _auth.Setup(a => a.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("SecurePass123!", "hashed-pw")).Returns(true);
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("random-tombstone-hash");
        return user;
    }

    [Fact]
    public async Task DeleteAccountAsync_UnknownUser_ThrowsInvalidCredentials()
    {
        _auth.Setup(a => a.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => CreateService().DeleteAccountAsync(Guid.NewGuid(), "SecurePass123!"));
    }

    [Fact]
    public async Task DeleteAccountAsync_WrongPassword_ThrowsAndDoesNotDelete()
    {
        var user = SetupUser("customer");
        _hasher.Setup(h => h.Verify("wrong", "hashed-pw")).Returns(false);

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => CreateService().DeleteAccountAsync(user.Id, "wrong"));

        _deletion.Verify(d => d.DeleteAccountAsync(It.IsAny<User>(), It.IsAny<IReadOnlyList<Guid>>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAccountAsync_Customer_AnonymizesUserAndDeletesTransactionally()
    {
        var user = SetupUser("customer");
        var originalEmail = user.Email;

        await CreateService().DeleteAccountAsync(user.Id, "SecurePass123!");

        // El user queda como tombstone sin datos personales (las reservas históricas lo referencian).
        Assert.Equal(AccountDeletionService.AnonymizedName, user.Name);
        Assert.NotEqual(originalEmail, user.Email);
        Assert.Contains(user.Id.ToString("N"), user.Email); // email sintético único, no personal
        Assert.Null(user.Phone);
        Assert.Equal("deleted", user.Status);
        Assert.NotNull(user.DeletedAt);
        Assert.Null(user.EmailVerifiedAt);
        Assert.NotEqual("hashed-pw", user.PasswordHash); // nadie puede volver a entrar

        // Borrado transaccional sin negocios que eliminar (es cliente).
        _deletion.Verify(d => d.DeleteAccountAsync(user,
            It.Is<IReadOnlyList<Guid>>(ids => ids.Count == 0),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAccountAsync_OwnerWithFutureReservations_Throws409AndDoesNotDelete()
    {
        var user = SetupUser("owner");
        var business = new Business { Id = Guid.NewGuid(), OwnerId = user.Id, Name = "Barbería" };
        _businesses.Setup(b => b.ListByOwnerAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([business]);
        _deletion.Setup(d => d.HasFutureActiveReservationsAsync(business.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<BusinessHasFutureReservationsException>(
            () => CreateService().DeleteAccountAsync(user.Id, "SecurePass123!"));

        Assert.Equal("Ana", user.Name); // no se tocó nada
        _deletion.Verify(d => d.DeleteAccountAsync(It.IsAny<User>(), It.IsAny<IReadOnlyList<Guid>>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAccountAsync_OwnerWithoutFutureReservations_DeletesBusinessToo()
    {
        var user = SetupUser("owner");
        var business = new Business { Id = Guid.NewGuid(), OwnerId = user.Id, Name = "Barbería" };
        _businesses.Setup(b => b.ListByOwnerAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([business]);
        _deletion.Setup(d => d.HasFutureActiveReservationsAsync(business.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await CreateService().DeleteAccountAsync(user.Id, "SecurePass123!");

        Assert.Equal("deleted", user.Status);
        _deletion.Verify(d => d.DeleteAccountAsync(user,
            It.Is<IReadOnlyList<Guid>>(ids => ids.Count == 1 && ids[0] == business.Id),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAccountAsync_Employee_DeletesWithoutBusinesses()
    {
        var user = SetupUser("employee");

        await CreateService().DeleteAccountAsync(user.Id, "SecurePass123!");

        // El desvinculado del staff lo hace el repositorio; aquí solo se comprueba
        // que no se intenta borrar ningún negocio (no es owner).
        _deletion.Verify(d => d.DeleteAccountAsync(user,
            It.Is<IReadOnlyList<Guid>>(ids => ids.Count == 0),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _businesses.Verify(b => b.ListByOwnerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
