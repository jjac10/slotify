using System.Security.Cryptography;
using System.Text;
using Moq;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Verificación de email (no bloqueante): al registrarse se "envía" un enlace con un
/// token de un solo uso (256 bits, solo hash SHA-256 en BD, 24 h de vida). El envío es
/// best-effort — el registro nunca falla por él. Verificar marca users.email_verified_at;
/// el reenvío regenera el token y rechaza cuentas ya verificadas.
/// </summary>
public class EmailVerificationServiceTests
{
    private readonly Mock<IAuthRepository> _auth = new();
    private readonly Mock<IEmailVerificationTokenRepository> _tokens = new();
    private readonly Mock<IAccountEmailSender> _mailer = new();

    private EmailVerificationService CreateService() =>
        new(_auth.Object, _tokens.Object, _mailer.Object);

    private static User NewUser(DateTime? verifiedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        Email = "ana@example.com",
        PasswordHash = "hash",
        Name = "Ana",
        Type = "customer",
        EmailVerifiedAt = verifiedAt,
    };

    private static string Sha256Hex(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private void SetupUser(User user) =>
        _auth.Setup(a => a.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

    // --- TrySendAsync (post-registro, best-effort) ---

    [Fact]
    public async Task TrySendAsync_UnverifiedUser_StoresOnlyHashAndSendsRawToken()
    {
        var user = NewUser();
        SetupUser(user);

        EmailVerificationToken? stored = null;
        _tokens.Setup(r => r.AddAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()))
            .Callback<EmailVerificationToken, CancellationToken>((t, _) => stored = t)
            .Returns(Task.CompletedTask);

        string? sentToken = null;
        _mailer.Setup(m => m.SendEmailVerificationAsync(user.Email, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, token, _) => sentToken = token)
            .Returns(Task.CompletedTask);

        await CreateService().TrySendAsync(user.Id);

        Assert.NotNull(stored);
        Assert.NotNull(sentToken);
        Assert.Equal(user.Id, stored!.UserId);
        // 256 bits aleatorios → base64url de 43 caracteres, nunca el hash.
        Assert.True(sentToken!.Length >= 43, "el token debe tener al menos 256 bits");
        Assert.Equal(Sha256Hex(sentToken), stored.TokenHash); // solo se persiste el hash SHA-256
        Assert.NotEqual(sentToken, stored.TokenHash);
        Assert.Null(stored.UsedAt);
        // Caducidad de 24 horas.
        var lifetime = stored.ExpiresAt - DateTime.UtcNow;
        Assert.InRange(lifetime, TimeSpan.FromHours(23), TimeSpan.FromHours(25));
    }

    [Fact]
    public async Task TrySendAsync_SenderFails_DoesNotThrow()
    {
        var user = NewUser();
        SetupUser(user);
        _mailer.Setup(m => m.SendEmailVerificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp caído"));

        // El registro NO debe fallar si el envío falla: no propaga ninguna excepción.
        await CreateService().TrySendAsync(user.Id);
    }

    [Fact]
    public async Task TrySendAsync_RepositoryFails_DoesNotThrow()
    {
        var user = NewUser();
        SetupUser(user);
        _tokens.Setup(r => r.AddAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bd caída"));

        await CreateService().TrySendAsync(user.Id);

        _mailer.Verify(m => m.SendEmailVerificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TrySendAsync_UnknownUser_DoesNothing()
    {
        _auth.Setup(a => a.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await CreateService().TrySendAsync(Guid.NewGuid());

        _tokens.Verify(r => r.AddAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _mailer.Verify(m => m.SendEmailVerificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TrySendAsync_AlreadyVerified_DoesNothing()
    {
        var user = NewUser(verifiedAt: DateTime.UtcNow.AddDays(-1));
        SetupUser(user);

        await CreateService().TrySendAsync(user.Id);

        _tokens.Verify(r => r.AddAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _mailer.Verify(m => m.SendEmailVerificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- VerifyAsync ---

    private (User user, string rawToken) SetupValidToken(
        DateTime? expiresAt = null, DateTime? usedAt = null, DateTime? verifiedAt = null)
    {
        var user = NewUser(verifiedAt);
        var rawToken = "raw-verification-token-with-enough-entropy";
        var entity = new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Sha256Hex(rawToken),
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddHours(12),
            UsedAt = usedAt,
        };
        _tokens.Setup(r => r.GetByHashAsync(entity.TokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        SetupUser(user);
        return (user, rawToken);
    }

    [Fact]
    public async Task VerifyAsync_ValidToken_MarksUserVerifiedAndTokenUsed()
    {
        var (user, rawToken) = SetupValidToken();

        await CreateService().VerifyAsync(rawToken);

        Assert.NotNull(user.EmailVerifiedAt);
        _auth.Verify(a => a.UpdateUserAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _tokens.Verify(r => r.UpdateAsync(
            It.Is<EmailVerificationToken>(t => t.UsedAt != null), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyAsync_ExpiredToken_Throws()
    {
        var (user, rawToken) = SetupValidToken(expiresAt: DateTime.UtcNow.AddMinutes(-1));

        await Assert.ThrowsAsync<InvalidEmailVerificationTokenException>(
            () => CreateService().VerifyAsync(rawToken));

        Assert.Null(user.EmailVerifiedAt);
        _auth.Verify(a => a.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VerifyAsync_AlreadyUsedToken_Throws()
    {
        var (_, rawToken) = SetupValidToken(usedAt: DateTime.UtcNow.AddMinutes(-5));

        await Assert.ThrowsAsync<InvalidEmailVerificationTokenException>(
            () => CreateService().VerifyAsync(rawToken));

        _auth.Verify(a => a.UpdateUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VerifyAsync_UnknownToken_Throws()
    {
        _tokens.Setup(r => r.GetByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmailVerificationToken?)null);

        await Assert.ThrowsAsync<InvalidEmailVerificationTokenException>(
            () => CreateService().VerifyAsync("token-que-no-existe"));
    }

    [Fact]
    public async Task VerifyAsync_UserAlreadyVerified_ConsumesTokenWithoutChangingDate()
    {
        var alreadyVerifiedAt = DateTime.UtcNow.AddDays(-3);
        var (user, rawToken) = SetupValidToken(verifiedAt: alreadyVerifiedAt);

        // Idempotente: consume el token pero no re-escribe la fecha original.
        await CreateService().VerifyAsync(rawToken);

        Assert.Equal(alreadyVerifiedAt, user.EmailVerifiedAt);
        _tokens.Verify(r => r.UpdateAsync(
            It.Is<EmailVerificationToken>(t => t.UsedAt != null), It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- ResendAsync (autenticado, explícito) ---

    [Fact]
    public async Task ResendAsync_UnverifiedUser_GeneratesFreshTokenAndSends()
    {
        var user = NewUser();
        SetupUser(user);

        var sent = new List<string>();
        _mailer.Setup(m => m.SendEmailVerificationAsync(user.Email, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, token, _) => sent.Add(token))
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.ResendAsync(user.Id);
        await service.ResendAsync(user.Id);

        Assert.Equal(2, sent.Count);
        Assert.NotEqual(sent[0], sent[1]); // cada reenvío regenera el token
        _tokens.Verify(r => r.AddAsync(It.IsAny<EmailVerificationToken>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ResendAsync_AlreadyVerified_Throws()
    {
        var user = NewUser(verifiedAt: DateTime.UtcNow.AddDays(-1));
        SetupUser(user);

        await Assert.ThrowsAsync<EmailAlreadyVerifiedException>(
            () => CreateService().ResendAsync(user.Id));

        _mailer.Verify(m => m.SendEmailVerificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- IsVerifiedAsync (para /auth/me) ---

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IsVerifiedAsync_ReflectsEmailVerifiedAt(bool verified)
    {
        var user = NewUser(verified ? DateTime.UtcNow : null);
        SetupUser(user);

        Assert.Equal(verified, await CreateService().IsVerifiedAsync(user.Id));
    }
}
