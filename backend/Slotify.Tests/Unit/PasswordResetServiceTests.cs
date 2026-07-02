using System.Security.Cryptography;
using System.Text;
using Moq;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Recuperación de contraseña: solicitar (token aleatorio, solo se persiste su hash,
/// email simulado) y restablecer (token válido/caducado/usado/desconocido, cambio de
/// contraseña e invalidación de refresh tokens).
/// </summary>
public class PasswordResetServiceTests
{
    private readonly Mock<IAuthRepository> _auth = new();
    private readonly Mock<IPasswordResetTokenRepository> _resetTokens = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<IPasswordResetEmailSender> _mailer = new();

    private PasswordResetService CreateService() =>
        new(_auth.Object, _resetTokens.Object, _hasher.Object, _refreshTokens.Object, _mailer.Object);

    private static User NewUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = "ana@example.com",
        PasswordHash = "old-hash",
        Name = "Ana",
        Type = "customer",
    };

    private static string Sha256Hex(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    // --- RequestResetAsync ---

    [Fact]
    public async Task RequestResetAsync_ExistingEmail_StoresOnlyHashAndSendsRawToken()
    {
        var user = NewUser();
        _auth.Setup(a => a.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        PasswordResetToken? stored = null;
        _resetTokens.Setup(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()))
            .Callback<PasswordResetToken, CancellationToken>((t, _) => stored = t)
            .Returns(Task.CompletedTask);

        string? sentToken = null;
        _mailer.Setup(m => m.SendAsync(user.Email, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, token, _) => sentToken = token)
            .Returns(Task.CompletedTask);

        await CreateService().RequestResetAsync(user.Email);

        Assert.NotNull(stored);
        Assert.NotNull(sentToken);
        Assert.Equal(user.Id, stored!.UserId);
        // 256 bits aleatorios → base64url de 43 caracteres, nunca el hash.
        Assert.True(sentToken!.Length >= 43, "el token debe tener al menos 256 bits");
        Assert.Equal(Sha256Hex(sentToken), stored.TokenHash); // solo se persiste el hash SHA-256
        Assert.NotEqual(sentToken, stored.TokenHash);
        Assert.Null(stored.UsedAt);
        // Caducidad de 1 hora.
        var lifetime = stored.ExpiresAt - DateTime.UtcNow;
        Assert.InRange(lifetime, TimeSpan.FromMinutes(55), TimeSpan.FromMinutes(65));
    }

    [Fact]
    public async Task RequestResetAsync_GeneratesDifferentTokensEachTime()
    {
        var user = NewUser();
        _auth.Setup(a => a.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var sent = new List<string>();
        _mailer.Setup(m => m.SendAsync(user.Email, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, token, _) => sent.Add(token))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.RequestResetAsync(user.Email);
        await service.RequestResetAsync(user.Email);

        Assert.Equal(2, sent.Count);
        Assert.NotEqual(sent[0], sent[1]);
    }

    [Fact]
    public async Task RequestResetAsync_UnknownEmail_DoesNothingSilently()
    {
        _auth.Setup(a => a.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // No lanza (anti-enumeración: el controller responde 200 genérico igualmente).
        await CreateService().RequestResetAsync("noexiste@example.com");

        _resetTokens.Verify(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _mailer.Verify(m => m.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- ResetPasswordAsync ---

    private (User user, string rawToken) SetupValidToken(DateTime? expiresAt = null, DateTime? usedAt = null)
    {
        var user = NewUser();
        var rawToken = "raw-token-value-with-enough-entropy-for-tests";
        var entity = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Sha256Hex(rawToken),
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddMinutes(30),
            UsedAt = usedAt,
        };
        _resetTokens.Setup(r => r.GetByHashAsync(entity.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        _auth.Setup(a => a.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return (user, rawToken);
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidToken_ChangesPasswordMarksUsedAndRevokesRefreshTokens()
    {
        var (user, rawToken) = SetupValidToken();
        _hasher.Setup(h => h.Hash("NewSecure123!")).Returns("new-hash");

        await CreateService().ResetPasswordAsync(rawToken, "NewSecure123!");

        Assert.Equal("new-hash", user.PasswordHash);
        _auth.Verify(a => a.UpdateUserAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _resetTokens.Verify(r => r.UpdateAsync(
            It.Is<PasswordResetToken>(t => t.UsedAt != null), It.IsAny<CancellationToken>()), Times.Once);
        // Cambiar la contraseña cierra las sesiones activas (refresh tokens).
        _refreshTokens.Verify(r => r.RevokeAllForUserAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_ExpiredToken_Throws()
    {
        var (_, rawToken) = SetupValidToken(expiresAt: DateTime.UtcNow.AddMinutes(-1));

        await Assert.ThrowsAsync<InvalidPasswordResetTokenException>(
            () => CreateService().ResetPasswordAsync(rawToken, "NewSecure123!"));

        _auth.Verify(a => a.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_AlreadyUsedToken_Throws()
    {
        var (_, rawToken) = SetupValidToken(usedAt: DateTime.UtcNow.AddMinutes(-5));

        await Assert.ThrowsAsync<InvalidPasswordResetTokenException>(
            () => CreateService().ResetPasswordAsync(rawToken, "NewSecure123!"));

        _auth.Verify(a => a.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_UnknownToken_Throws()
    {
        _resetTokens.Setup(r => r.GetByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PasswordResetToken?)null);

        await Assert.ThrowsAsync<InvalidPasswordResetTokenException>(
            () => CreateService().ResetPasswordAsync("token-que-no-existe", "NewSecure123!"));
    }

    [Fact]
    public async Task ResetPasswordAsync_WeakPassword_ThrowsWithoutTouchingToken()
    {
        var (_, rawToken) = SetupValidToken();

        await Assert.ThrowsAsync<WeakPasswordException>(
            () => CreateService().ResetPasswordAsync(rawToken, "weak"));

        _resetTokens.Verify(r => r.UpdateAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _auth.Verify(a => a.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
