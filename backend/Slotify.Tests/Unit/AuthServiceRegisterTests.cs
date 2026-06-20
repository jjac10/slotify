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
    private readonly Mock<IRefreshTokenRepository> _refresh = new();
    private readonly Mock<IGuestRepository> _guests = new();
    private readonly Mock<IBlindIndex> _blindIndex = new();
    private readonly Mock<IBusinessRepository> _businesses = new();

    private AuthService CreateService() =>
        new(_auth.Object, _tiers.Object, _hasher.Object, _tokens.Object, _refresh.Object, _guests.Object, _blindIndex.Object, _businesses.Object);

    private static readonly PricingTier FreeTier = new()
    {
        Id = Guid.NewGuid(),
        Code = "free",
        Name = "Free",
        MaxStaff = 1,
    };

    [Fact]
    public async Task RegisterOwnerAsync_CreatesOwnerWithBusinessAndStaff_AndReturnsTokens()
    {
        _auth.Setup(a => a.EmailExistsAsync("owner@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _tiers.Setup(t => t.GetByCodeAsync("free", It.IsAny<CancellationToken>()))
            .ReturnsAsync(FreeTier);
        _hasher.Setup(h => h.Hash("SecurePass123!")).Returns("hashed-pw");
        _tokens.Setup(t => t.CreateAccessToken(It.IsAny<User>())).Returns("access-token");
        _tokens.Setup(t => t.CreateRefreshToken()).Returns("refresh-token");
        _refresh.Setup(r => r.IssueAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        User? savedUser = null;
        Business? savedBusiness = null;
        Staff? savedStaff = null;
        List<BusinessHour>? savedHours = null;
        _auth.Setup(a => a.RegisterOwnerAsync(
                It.IsAny<User>(), It.IsAny<Business>(), It.IsAny<Staff>(), It.IsAny<IEnumerable<BusinessHour>>(), It.IsAny<CancellationToken>()))
            .Callback<User, Business, Staff, IEnumerable<BusinessHour>, CancellationToken>((u, b, s, h, _) =>
                { savedUser = u; savedBusiness = b; savedStaff = s; savedHours = h.ToList(); })
            .Returns(Task.CompletedTask);

        var request = new RegisterOwnerRequest("owner@example.com", "SecurePass123!", "Pepe", "Barbería Pepe");
        var result = await CreateService().RegisterOwnerAsync(request);

        _auth.Verify(a => a.RegisterOwnerAsync(
            It.IsAny<User>(), It.IsAny<Business>(), It.IsAny<Staff>(), It.IsAny<IEnumerable<BusinessHour>>(), It.IsAny<CancellationToken>()), Times.Once);

        // Horario por defecto: L–V abierto 09:00–17:00, S/D cerrado (una fila por día).
        Assert.NotNull(savedHours);
        Assert.Equal(7, savedHours!.Count);
        Assert.All(savedHours, h => Assert.Equal(savedBusiness!.Id, h.BusinessId));
        foreach (var weekday in new[] { 1, 2, 3, 4, 5 })
        {
            var row = savedHours.Single(h => h.DayOfWeek == weekday);
            Assert.False(row.IsClosed);
            Assert.Equal(new TimeOnly(9, 0), row.OpeningTime);
            Assert.Equal(new TimeOnly(17, 0), row.ClosingTime);
        }
        foreach (var weekend in new[] { 0, 6 })
            Assert.True(savedHours.Single(h => h.DayOfWeek == weekend).IsClosed);

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

        // El refresh token se persiste para el usuario creado.
        _refresh.Verify(r => r.IssueAsync(savedUser.Id, "refresh-token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterOwnerAsync_WeakPassword_Throws_AndDoesNotPersist()
    {
        // Contraseña débil: debe rechazarse antes de tocar la BD.
        var request = new RegisterOwnerRequest("owner@example.com", "weak", "Pepe", "Barbería Pepe");

        await Assert.ThrowsAsync<WeakPasswordException>(() => CreateService().RegisterOwnerAsync(request));

        _auth.Verify(a => a.RegisterOwnerAsync(
            It.IsAny<User>(), It.IsAny<Business>(), It.IsAny<Staff>(), It.IsAny<IEnumerable<BusinessHour>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterOwnerAsync_WhenEmailExists_Throws_AndDoesNotPersist()
    {
        _auth.Setup(a => a.EmailExistsAsync("taken@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new RegisterOwnerRequest("taken@example.com", "SecurePass123!", "Pepe", "Barbería Pepe");

        await Assert.ThrowsAsync<EmailAlreadyExistsException>(() => CreateService().RegisterOwnerAsync(request));

        _auth.Verify(a => a.RegisterOwnerAsync(
            It.IsAny<User>(), It.IsAny<Business>(), It.IsAny<Staff>(), It.IsAny<IEnumerable<BusinessHour>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
