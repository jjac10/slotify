using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Slotify.API;
using Slotify.Domain.Exceptions;

namespace Slotify.Tests.Unit;

/// <summary>
/// Manejo de errores estándar: un IExceptionHandler global mapea cada excepción de
/// dominio a su respuesta {error, message} con el status HTTP correcto, para que los
/// controllers no necesiten try/catch repetitivos. Lo que no reconoce lo deja pasar
/// (500). Los casos con slug contextual (invalid_password, webhook) siguen teniendo
/// catch local en su endpoint.
/// </summary>
public class DomainExceptionHandlerTests
{
    private static async Task<(bool handled, int status, JsonElement? body)> HandleAsync(Exception exception)
    {
        var context = new DefaultHttpContext();
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        var handled = await new DomainExceptionHandler().TryHandleAsync(context, exception, CancellationToken.None);

        if (!handled) return (false, 0, null);
        buffer.Position = 0;
        using var json = await JsonDocument.ParseAsync(buffer);
        return (handled, context.Response.StatusCode, json.RootElement.Clone());
    }

    [Fact]
    public async Task Handles_NotFound_Exceptions_As404WithSlug()
    {
        var (handled, status, body) = await HandleAsync(new BusinessNotFoundException(Guid.NewGuid()));

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Equal("business_not_found", body!.Value.GetProperty("error").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.Value.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Handles_Forbidden_As403()
    {
        var (handled, status, body) = await HandleAsync(new NotBusinessOwnerException());

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status403Forbidden, status);
        Assert.Equal("forbidden", body!.Value.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData(typeof(PaymentRequiredException), StatusCodes.Status409Conflict, "payment_required")]
    [InlineData(typeof(AlreadyPremiumException), StatusCodes.Status409Conflict, "already_premium")]
    [InlineData(typeof(InvalidPlanException), StatusCodes.Status400BadRequest, "invalid_plan")]
    public async Task Handles_RepresentativeMappings(Type exceptionType, int expectedStatus, string expectedSlug)
    {
        var exception = exceptionType == typeof(InvalidPlanException)
            ? new InvalidPlanException("gold")
            : (Exception)Activator.CreateInstance(exceptionType)!;

        var (handled, status, body) = await HandleAsync(exception);

        Assert.True(handled);
        Assert.Equal(expectedStatus, status);
        Assert.Equal(expectedSlug, body!.Value.GetProperty("error").GetString());
    }

    [Fact]
    public async Task WeakPassword_IncludesValidationDetails()
    {
        var (handled, status, body) = await HandleAsync(new WeakPasswordException(["mínimo 8 caracteres", "una mayúscula"]));

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal("weak_password", body!.Value.GetProperty("error").GetString());
        Assert.Equal(2, body.Value.GetProperty("details").GetArrayLength());
    }

    [Fact]
    public async Task UnknownException_IsNotHandled()
    {
        var (handled, _, _) = await HandleAsync(new InvalidOperationException("algo inesperado"));

        Assert.False(handled);
    }
}
