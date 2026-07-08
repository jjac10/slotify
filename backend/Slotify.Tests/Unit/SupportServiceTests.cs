using Moq;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Formulario de contacto/soporte: validación (campos obligatorios con trim, formato de
/// email, longitudes máximas) y delegación en el sender simulado. Si la validación falla,
/// el sender NUNCA se invoca.
/// </summary>
public class SupportServiceTests
{
    private readonly Mock<ISupportEmailSender> _mailer = new();

    private SupportService CreateService() => new(_mailer.Object);

    private Task Send(string? name, string? email, string? message) =>
        CreateService().SendContactMessageAsync(name, email, message);

    // --- Camino feliz ---

    [Fact]
    public async Task SendContactMessageAsync_ValidData_CallsSenderOnceWithSameData()
    {
        await Send("Ana García", "ana@example.com", "Hola, tengo una duda sobre las reservas.");

        _mailer.Verify(m => m.SendContactMessageAsync(
            "Ana García", "ana@example.com", "Hola, tengo una duda sobre las reservas.",
            It.IsAny<CancellationToken>()), Times.Once);
        _mailer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendContactMessageAsync_TrimsFieldsBeforeSending()
    {
        await Send("  Ana  ", " ana@example.com ", "  Mensaje con espacios.  ");

        _mailer.Verify(m => m.SendContactMessageAsync(
            "Ana", "ana@example.com", "Mensaje con espacios.", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendContactMessageAsync_MaxAllowedLengths_AreAccepted()
    {
        // Justo en el límite: name 100, email 254, message 2000.
        var name = new string('a', 100);
        var email = new string('a', 244) + "@test.com"; // 254 en total
        var message = new string('m', 2000);

        await Send(name, email, message);

        _mailer.Verify(m => m.SendContactMessageAsync(
            name, email, message, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendContactMessageAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        await CreateService().SendContactMessageAsync("Ana", "ana@example.com", "Hola.", cts.Token);

        _mailer.Verify(m => m.SendContactMessageAsync("Ana", "ana@example.com", "Hola.", cts.Token), Times.Once);
    }

    // --- Validación: campos obligatorios ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendContactMessageAsync_MissingName_ThrowsAndNeverSends(string? name)
    {
        await Assert.ThrowsAsync<InvalidContactMessageException>(
            () => Send(name, "ana@example.com", "Hola."));

        _mailer.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendContactMessageAsync_MissingEmail_ThrowsAndNeverSends(string? email)
    {
        await Assert.ThrowsAsync<InvalidContactMessageException>(
            () => Send("Ana", email, "Hola."));

        _mailer.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendContactMessageAsync_MissingMessage_ThrowsAndNeverSends(string? message)
    {
        await Assert.ThrowsAsync<InvalidContactMessageException>(
            () => Send("Ana", "ana@example.com", message));

        _mailer.VerifyNoOtherCalls();
    }

    // --- Validación: formato de email ---

    [Theory]
    [InlineData("no-es-un-email")]
    [InlineData("sin-arroba.com")]
    [InlineData("dos@arrobas@test.com")]
    [InlineData("con espacios@test.com")]
    [InlineData("@sinlocal.com")]
    [InlineData("sindominio@")]
    public async Task SendContactMessageAsync_InvalidEmailFormat_ThrowsAndNeverSends(string email)
    {
        await Assert.ThrowsAsync<InvalidContactMessageException>(
            () => Send("Ana", email, "Hola."));

        _mailer.VerifyNoOtherCalls();
    }

    // --- Validación: longitudes máximas ---

    [Fact]
    public async Task SendContactMessageAsync_NameTooLong_ThrowsAndNeverSends()
    {
        await Assert.ThrowsAsync<InvalidContactMessageException>(
            () => Send(new string('a', 101), "ana@example.com", "Hola."));

        _mailer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendContactMessageAsync_EmailTooLong_ThrowsAndNeverSends()
    {
        var email = new string('a', 246) + "@test.com"; // 255 > 254

        await Assert.ThrowsAsync<InvalidContactMessageException>(
            () => Send("Ana", email, "Hola."));

        _mailer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendContactMessageAsync_MessageTooLong_ThrowsAndNeverSends()
    {
        await Assert.ThrowsAsync<InvalidContactMessageException>(
            () => Send("Ana", "ana@example.com", new string('m', 2001)));

        _mailer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendContactMessageAsync_AllFieldsInvalid_ReportsEveryError()
    {
        var ex = await Assert.ThrowsAsync<InvalidContactMessageException>(
            () => Send("", "no-es-un-email", ""));

        // Un error por campo inválido, para que el frontend pueda mostrarlos todos.
        Assert.Equal(3, ex.Errors.Count);
    }
}
