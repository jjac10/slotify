using Microsoft.Extensions.Logging;
using Slotify.Domain.Interfaces;

namespace Slotify.Infrastructure.Notifications;

/// <summary>URL pública del frontend (para construir enlaces en emails). Sección "Frontend".</summary>
public class FrontendOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5173";
}

/// <summary>
/// Sender simulado del email de recuperación (mismo seam que
/// <see cref="LoggedNotificationSender"/>): no envía de verdad, registra en el log el
/// enlace de restablecimiento. En producción se sustituye por un proveedor real de
/// email sin tocar <c>PasswordResetService</c>.
/// </summary>
public class LoggedPasswordResetEmailSender(
    ILogger<LoggedPasswordResetEmailSender> logger,
    FrontendOptions frontend) : IPasswordResetEmailSender
{
    public Task SendAsync(string email, string token, CancellationToken ct = default)
    {
        var link = $"{frontend.BaseUrl.TrimEnd('/')}/restablecer?token={Uri.EscapeDataString(token)}";
        logger.LogInformation(
            "[NOTIFY:email] (password_reset) → {Recipient}: Restablece tu contraseña de Slotify (caduca en 1 hora): {Link}",
            email, link);
        return Task.CompletedTask;
    }
}
