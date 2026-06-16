using Moq;
using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>Login (RF-AUTH-002): credenciales válidas → tokens; inválidas → excepción.</summary>
public class AuthServiceLoginTests
{
    private readonly Mock<IAuthRepository> _auth = new();
    private readonly Mock<ITierRepository> _tiers = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IRefreshTokenRepository> _refresh = new();

    private AuthService CreateService() => new(_auth.Object, _tiers.Object, _hasher.Object, _tokens.Object, _refresh.Object);

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokens()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "owner@example.com",
            PasswordHash = "hashed-pw",
            Name = "Pepe",
            Type = "owner",
        };
        _auth.Setup(a => a.GetByEmailAsync("owner@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("SecurePass123!", "hashed-pw")).Returns(true);
        _tokens.Setup(t => t.CreateAccessToken(user)).Returns("access-token");
        _tokens.Setup(t => t.CreateRefreshToken()).Returns("refresh-token");
        _refresh.Setup(r => r.IssueAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateService().LoginAsync(new LoginRequest("owner@example.com", "SecurePass123!"));

        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("access-token", result.AccessToken);
        Assert.Equal("refresh-token", result.RefreshToken);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ThrowsInvalidCredentials()
    {
        _auth.Setup(a => a.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => CreateService().LoginAsync(new LoginRequest("nope@example.com", "x")));
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsInvalidCredentials()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "owner@example.com", PasswordHash = "hashed-pw", Name = "Pepe" };
        _auth.Setup(a => a.GetByEmailAsync("owner@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("wrong", "hashed-pw")).Returns(false);

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => CreateService().LoginAsync(new LoginRequest("owner@example.com", "wrong")));
    }
}
