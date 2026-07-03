using Microsoft.Extensions.Logging;
using Slotify.Domain.Interfaces;

namespace Slotify.Infrastructure.Notifications;

/// <summary>URL pública del frontend (para construir enlaces en emails). Sección "Frontend".</summary>
public class FrontendOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5173";
}

/// <summary>
/// Sender simulado de los emails de cuenta (mismo seam que
/// <see cref="LoggedNotificationSender"/>): no envía de verdad, registra en el log el
/// enlace de restablecimiento/verificación. En producción se sustituye por un
/// proveedor real de email sin tocar los servicios de dominio.
/// </summary>
public class LoggedAccountEmailSender(
    ILogger<LoggedAccountEmailSender> logger,
    FrontendOptions frontend) : IAccountEmailSender
{
    public Task SendPasswordResetAsync(string email, string token, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[NOTIFY:email] (password_reset) → {Recipient}: Restablece tu contraseña de Slotify (caduca en 1 hora): {Link}",
            email, Link("restablecer", token));
        return Task.CompletedTask;
    }

    public Task SendEmailVerificationAsync(string email, string token, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[NOTIFY:email] (email_verification) → {Recipient}: Verifica tu email de Slotify (caduca en 24 horas): {Link}",
            email, Link("verificar-email", token));
        return Task.CompletedTask;
    }

    private string Link(string path, string token)
        => $"{frontend.BaseUrl.TrimEnd('/')}/{path}?token={Uri.EscapeDataString(token)}";
}
