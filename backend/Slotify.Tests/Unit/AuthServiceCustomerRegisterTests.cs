using Moq;
using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>Registro de cliente (sin negocio): crea user con type='customer'.</summary>
public class AuthServiceCustomerRegisterTests
{
    private readonly Mock<IAuthRepository> _auth = new();
    private readonly Mock<ITierRepository> _tiers = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IRefreshTokenRepository> _refresh = new();

    private AuthService CreateService() => new(_auth.Object, _tiers.Object, _hasher.Object, _tokens.Object, _refresh.Object);

    [Fact]
    public async Task RegisterCustomerAsync_CreatesCustomer_AndReturnsTokens()
    {
        _auth.Setup(a => a.EmailExistsAsync("c@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _hasher.Setup(h => h.Hash("SecurePass123!")).Returns("hashed-pw");
        _tokens.Setup(t => t.CreateAccessToken(It.IsAny<User>())).Returns("access-token");
        _tokens.Setup(t => t.CreateRefreshToken()).Returns("refresh-token");
        _refresh.Setup(r => r.IssueAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        User? saved = null;
        _auth.Setup(a => a.AddUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => saved = u)
            .Returns(Task.CompletedTask);

        var result = await CreateService().RegisterCustomerAsync(
            new RegisterCustomerRequest("c@example.com", "SecurePass123!", "Ana"));

        _auth.Verify(a => a.AddUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(saved);
        Assert.Equal("c@example.com", saved!.Email);
        Assert.Equal("hashed-pw", saved.PasswordHash);
        Assert.Equal("customer", saved.Type);

        Assert.Equal(saved.Id, result.UserId);
        Assert.Null(result.BusinessId); // customer no tiene negocio
        Assert.Equal("access-token", result.AccessToken);
        Assert.Equal("refresh-token", result.RefreshToken);
        // No debe crear negocio ni owner-staff.
        _auth.Verify(a => a.RegisterOwnerAsync(
            It.IsAny<User>(), It.IsAny<Business>(), It.IsAny<Staff>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterCustomerAsync_WeakPassword_Throws_AndDoesNotPersist()
    {
        await Assert.ThrowsAsync<WeakPasswordException>(() => CreateService().RegisterCustomerAsync(
            new RegisterCustomerRequest("c@example.com", "weak", "Ana")));

        _auth.Verify(a => a.AddUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterCustomerAsync_WhenEmailExists_Throws_AndDoesNotPersist()
    {
        _auth.Setup(a => a.EmailExistsAsync("taken@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<EmailAlreadyExistsException>(() => CreateService().RegisterCustomerAsync(
            new RegisterCustomerRequest("taken@example.com", "SecurePass123!", "Ana")));

        _auth.Verify(a => a.AddUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
