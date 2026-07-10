using System.Collections.Concurrent;
using Slotify.Domain.Interfaces;

namespace Slotify.Tests.Integration;

/// <summary>
/// Sender de test: captura en memoria los códigos OTP de invitado por canal (mismo
/// patrón que <see cref="CapturingAccountEmailSender"/>). Los tests leen de aquí el
/// código, igual que haría un invitado real desde su email/teléfono — el código JAMÁS
/// viaja en la respuesta HTTP.
/// </summary>
public sealed class CapturingGuestOtpSender : IGuestOtpSender
{
    public ConcurrentQueue<(string Contact, string Code)> EmailCodes { get; } = new();
    public ConcurrentQueue<(string Contact, string Code)> PhoneCodes { get; } = new();

    public Task SendEmailOtpAsync(string email, string code, CancellationToken ct = default)
    {
        EmailCodes.Enqueue((email, code));
        return Task.CompletedTask;
    }

    public Task SendPhoneOtpAsync(string phoneE164, string code, CancellationToken ct = default)
    {
        PhoneCodes.Enqueue((phoneE164, code));
        return Task.CompletedTask;
    }

    /// <summary>Último código enviado a un contacto (email o teléfono), o null.</summary>
    public string? LastCodeFor(string contact)
    {
        var all = EmailCodes.Concat(PhoneCodes).Where(c => c.Contact == contact).ToList();
        return all.Count > 0 ? all[^1].Code : null;
    }
}
