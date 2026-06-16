using Moq;
using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Registro de propietario (RF-AUTH-001 / API.md POST /auth/register): crea user
/// (type=owner) + negocio en plan Free + su owner-staff, y emite tokens.
/// </summary>
public class AuthServiceRegisterTests
{
    private readonly Mock<IAuthRepository> _auth = new();
    private readonly Mock<ITierRepository> _tiers = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokens = new();

    private AuthService CreateService() => new(_auth.Object, _tiers.Object, _hasher.Object, _tokens.Object);

    private static readonly PricingTier FreeTier = new()
    {
        Id = Guid.NewGuid(),
        Code = "free",
        Name = "Free",
        MaxStaff = 1,
    };

    [Fact]
    public async Task RegisterAsync_CreatesOwnerWithBusinessAndStaff_AndReturnsTokens()
    {
        _auth.Setup(a => a.EmailExistsAsync("owner@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _tiers.Setup(t => t.GetByCodeAsync("free", It.IsAny<CancellationToken>()))
            .ReturnsAsync(FreeTier);
        _hasher.Setup(h => h.Hash("SecurePass123!")).Returns("hashed-pw");
        _tokens.Setup(t => t.CreateAccessToken(It.IsAny<User>())).Returns("access-token");
        _tokens.Setup(t => t.CreateRefreshToken()).Returns("refresh-token");

        User? savedUser = null;
        Business? savedBusiness = null;
        Staff? savedStaff = null;
        _auth.Setup(a => a.RegisterOwnerAsync(
                It.IsAny<User>(), It.IsAny<Business>(), It.IsAny<Staff>(), It.IsAny<CancellationToken>()))
            .Callback<User, Business, Staff, CancellationToken>((u, b, s, _) =>
                { savedUser = u; savedBusiness = b; savedStaff = s; })
            .Returns(Task.CompletedTask);

        var request = new RegisterRequest("owner@example.com", "SecurePass123!", "Pepe", "Barbería Pepe");
        var result = await CreateService().RegisterAsync(request);

        _auth.Verify(a => a.RegisterOwnerAsync(
            It.IsAny<User>(), It.IsAny<Business>(), It.IsAny<Staff>(), It.IsAny<CancellationToken>()), Times.Once);

        Assert.NotNull(savedUser);
        Assert.Equal("owner@example.com", savedUser!.Email);
        Assert.Equal("hashed-pw", savedUser.PasswordHash);
        Assert.Equal("owner", savedUser.Type);

        Assert.NotNull(savedBusiness);
        Assert.Equal(savedUser.Id, savedBusiness!.OwnerId);
        Assert.Equal(FreeTier.Id, savedBusiness.TierId);
        Assert.Equal("Barbería Pepe", savedBusiness.Name);

        Assert.NotNull(savedStaff);
        Assert.Equal("owner", savedStaff!.Role);
        Assert.Equal(savedBusiness.Id, savedStaff.BusinessId);
        Assert.Equal(savedUser.Id, savedStaff.UserId);
        Assert.Equal("Pepe", savedStaff.Name);

        Assert.Equal(savedUser.Id, result.UserId);
        Assert.Equal(savedBusiness.Id, result.BusinessId);
        Assert.Equal("access-token", result.AccessToken);
        Assert.Equal("refresh-token", result.RefreshToken);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailExists_Throws_AndDoesNotPersist()
    {
        _auth.Setup(a => a.EmailExistsAsync("taken@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new RegisterRequest("taken@example.com", "SecurePass123!", "Pepe", "Barbería Pepe");

        await Assert.ThrowsAsync<EmailAlreadyExistsException>(() => CreateService().RegisterAsync(request));

        _auth.Verify(a => a.RegisterOwnerAsync(
            It.IsAny<User>(), It.IsAny<Business>(), It.IsAny<Staff>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
