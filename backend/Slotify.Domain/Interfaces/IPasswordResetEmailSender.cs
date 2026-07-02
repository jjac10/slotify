namespace Slotify.Domain.Interfaces;

/// <summary>
/// "Envía" el email de recuperación de contraseña con el token en claro. En el TFM la
/// implementación es simulada (se registra en el log con el enlace de restablecimiento);
/// en producción se sustituye por un proveedor real de email sin tocar
/// <c>PasswordResetService</c> (mismo seam que <see cref="INotificationSender"/>).
/// </summary>
public interface IPasswordResetEmailSender
{
    Task SendAsync(string email, string token, CancellationToken ct = default);
}
