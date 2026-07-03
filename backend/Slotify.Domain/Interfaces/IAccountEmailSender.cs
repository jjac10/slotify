namespace Slotify.Domain.Interfaces;

/// <summary>
/// "Envía" los emails transaccionales de cuenta (recuperación de contraseña y
/// verificación de email) con el token en claro. En el TFM la implementación es
/// simulada (registra en el log el enlace); en producción se sustituye por un
/// proveedor real de email sin tocar los servicios de dominio (mismo seam que
/// <see cref="INotificationSender"/>). Una sola interfaz para todos los emails de
/// cuenta: mismo mecanismo de entrega y un único punto de swap.
/// </summary>
public interface IAccountEmailSender
{
    /// <summary>Email de recuperación de contraseña (enlace /restablecer?token=…).</summary>
    Task SendPasswordResetAsync(string email, string token, CancellationToken ct = default);

    /// <summary>Email de verificación de cuenta (enlace /verificar-email?token=…).</summary>
    Task SendEmailVerificationAsync(string email, string token, CancellationToken ct = default);
}
