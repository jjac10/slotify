using System.Security.Cryptography;
using System.Text;
using Moq;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Verificación de identidad del invitado (OTP): antes de ver/gestionar reservas por
/// contacto hay que pedir un código de 6 dígitos que viaja por email (real vía SMTP) o
/// por teléfono (seam preparado, simulado hoy). Solo se persiste el hash del código,
/// caduca a los 10 minutos y admite un máximo de intentos fallidos. Anti-enumeración:
/// pedir código nunca revela si el contacto tiene reservas.
/// </summary>
public class GuestOtpServiceTests
{
    private readonly Mock<IGuestOtpRepository> _codes = new();
    private readonly Mock<IGuestOtpSender> _sender = new();
    private readonly Mock<IBlindIndex> _blindIndex = new();

    private readonly List<GuestOtpCode> _stored = [];

    public GuestOtpServiceTests()
    {
        _blindIndex.Setup(b => b.Compute(It.IsAny<string>())).Returns<string>(v => $"hash({v})");
        _codes.Setup(r => r.AddAsync(It.IsAny<GuestOtpCode>(), It.IsAny<CancellationToken>()))
            .Callback<GuestOtpCode, CancellationToken>((c, _) => _stored.Add(c))
            .Returns(Task.CompletedTask);
    }

    private GuestOtpService CreateService() => new(_codes.Object, _sender.Object, _blindIndex.Object);

    private string? SentEmailCode;
    private string? SentPhoneCode;

    private void CaptureSends()
    {
        _sender.Setup(s => s.SendEmailOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, code, _) => SentEmailCode = code)
            .Returns(Task.CompletedTask);
        _sender.Setup(s => s.SendPhoneOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, code, _) => SentPhoneCode = code)
            .Returns(Task.CompletedTask);
    }

    // --- Pedir código ---

    [Fact]
    public async Task RequestCode_Email_SendsSixDigitCodeByEmail_AndStoresOnlyHash()
    {
        CaptureSends();

        await CreateService().RequestCodeAsync("Ana@Example.com ");

        Assert.NotNull(SentEmailCode);
        Assert.Matches(@"^\d{6}$", SentEmailCode!);
        var stored = Assert.Single(_stored);
        Assert.DoesNotContain(SentEmailCode!, stored.CodeHash); // hash, nunca el código en claro
        Assert.Equal("hash(ana@example.com)", stored.ContactHash); // contacto normalizado
        Assert.True(stored.ExpiresAt > DateTime.UtcNow.AddMinutes(9));
        _sender.Verify(s => s.SendPhoneOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestCode_Phone_SendsCodeByPhoneChannel()
    {
        CaptureSends();

        await CreateService().RequestCodeAsync("+34 600 111 222");

        Assert.NotNull(SentPhoneCode);
        Assert.Matches(@"^\d{6}$", SentPhoneCode!);
        _sender.Verify(s => s.SendEmailOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestCode_BlankContact_DoesNothing()
    {
        await CreateService().RequestCodeAsync("  ");

        Assert.Empty(_stored);
        _sender.VerifyNoOtherCalls();
    }

    // --- Verificar código ---

    /// <summary>Mismo formato que persiste el servicio (SHA-256 hex de .NET).</summary>
    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private GuestOtpCode ActiveCode(string contactNormalized, string code, int attempts = 0, TimeSpan? age = null) => new()
    {
        Id = Guid.NewGuid(),
        ContactHash = $"hash({contactNormalized})",
        CodeHash = Sha256Hex(code),
        Attempts = attempts,
        ExpiresAt = DateTime.UtcNow.Add(GuestOtpService.CodeLifetime) - (age ?? TimeSpan.Zero),
        CreatedAt = DateTime.UtcNow - (age ?? TimeSpan.Zero),
    };

    private void StoredCode(GuestOtpCode entity)
    {
        _codes.Setup(r => r.GetLatestByContactHashAsync(entity.ContactHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
    }

    [Fact]
    public async Task Verify_CorrectCode_ReturnsTrue_AndIsReusableWithinLifetime()
    {
        StoredCode(ActiveCode("ana@example.com", "123456"));

        var service = CreateService();
        Assert.True(await service.VerifyAsync("ana@example.com", "123456"));
        // Reutilizable dentro de la ventana: ver reservas y después cancelar con el mismo código.
        Assert.True(await service.VerifyAsync("ana@example.com", "123456"));
    }

    [Fact]
    public async Task Verify_WrongCode_ReturnsFalse_AndIncrementsAttempts()
    {
        var entity = ActiveCode("ana@example.com", "123456");
        StoredCode(entity);

        Assert.False(await CreateService().VerifyAsync("ana@example.com", "000000"));

        Assert.Equal(1, entity.Attempts);
        _codes.Verify(r => r.UpdateAsync(entity, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Verify_TooManyFailedAttempts_RejectsEvenTheCorrectCode()
    {
        StoredCode(ActiveCode("ana@example.com", "123456", attempts: GuestOtpService.MaxAttempts));

        Assert.False(await CreateService().VerifyAsync("ana@example.com", "123456"));
    }

    [Fact]
    public async Task Verify_ExpiredCode_ReturnsFalse()
    {
        StoredCode(ActiveCode("ana@example.com", "123456", age: GuestOtpService.CodeLifetime + TimeSpan.FromMinutes(1)));

        Assert.False(await CreateService().VerifyAsync("ana@example.com", "123456"));
    }

    [Fact]
    public async Task Verify_NoCodeRequested_ReturnsFalse()
    {
        Assert.False(await CreateService().VerifyAsync("ana@example.com", "123456"));
    }

    [Theory]
    [InlineData(null, "123456")]
    [InlineData("ana@example.com", null)]
    [InlineData("", "")]
    public async Task Verify_BlankInputs_ReturnFalse(string? contact, string? code)
    {
        Assert.False(await CreateService().VerifyAsync(contact, code));
    }

    [Fact]
    public async Task Verify_NormalizesContactLikeRequest()
    {
        StoredCode(ActiveCode("+34600111222", "123456"));

        // El invitado escribió el teléfono con espacios: mismo blind index normalizado.
        Assert.True(await CreateService().VerifyAsync("+34 600 111 222", "123456"));
    }
}
