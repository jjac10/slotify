using System.Collections.Concurrent;
using Slotify.Domain.Interfaces;

namespace Slotify.Tests.Integration;

/// <summary>
/// Sender de test: captura en memoria los "emails" del formulario de contacto/soporte
/// (mismo patrón que <see cref="CapturingAccountEmailSender"/>). Los tests verifican
/// aquí qué se "envió" al equipo de Slotify.
/// </summary>
public sealed class CapturingSupportEmailSender : ISupportEmailSender
{
    public ConcurrentQueue<(string Name, string Email, string Message)> ContactMessages { get; } = new();

    public Task SendContactMessageAsync(string name, string email, string message, CancellationToken ct = default)
    {
        ContactMessages.Enqueue((name, email, message));
        return Task.CompletedTask;
    }
}
