using System.Collections.Concurrent;
using Slotify.Domain.Interfaces;

namespace Slotify.Tests.Integration;

/// <summary>
/// Sender de test: captura en memoria los "emails" de cuenta (recuperación de
/// contraseña y verificación de email) por separado. Los tests leen de aquí el token,
/// igual que haría un usuario real desde su bandeja — el token JAMÁS viaja en la
/// respuesta HTTP.
/// </summary>
public sealed class CapturingAccountEmailSender : IAccountEmailSender
{
    public ConcurrentQueue<(string Email, string Token)> PasswordResets { get; } = new();
    public ConcurrentQueue<(string Email, string Token)> Verifications { get; } = new();

    /// <summary>Simula un proveedor de email caído para los envíos de verificación.</summary>
    public bool FailEmailVerification { get; set; }

    public Task SendPasswordResetAsync(string email, string token, CancellationToken ct = default)
    {
        PasswordResets.Enqueue((email, token));
        return Task.CompletedTask;
    }

    public Task SendEmailVerificationAsync(string email, string token, CancellationToken ct = default)
    {
        if (FailEmailVerification)
            throw new InvalidOperationException("proveedor de email caído (simulado)");

        Verifications.Enqueue((email, token));
        return Task.CompletedTask;
    }
}
