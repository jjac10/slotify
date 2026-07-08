using System.Net.Mail;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Formulario público de contacto/soporte: valida los datos del remitente (campos
/// obligatorios con trim, formato de email, longitudes máximas) y delega el "envío" en
/// <see cref="ISupportEmailSender"/>. No persiste nada en BD.
/// </summary>
public class SupportService(ISupportEmailSender mailer)
{
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 254; // RFC 5321
    public const int MessageMaxLength = 2000;

    /// <summary>
    /// Valida y "envía" el mensaje de contacto. Lanza
    /// <see cref="InvalidContactMessageException"/> (con un error por campo inválido)
    /// sin invocar el sender si algún campo no es válido.
    /// </summary>
    public async Task SendContactMessageAsync(
        string? name, string? email, string? message, CancellationToken ct = default)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        var trimmedEmail = email?.Trim() ?? string.Empty;
        var trimmedMessage = message?.Trim() ?? string.Empty;

        var errors = new List<string>();
        if (trimmedName.Length == 0)
            errors.Add("El nombre es obligatorio.");
        else if (trimmedName.Length > NameMaxLength)
            errors.Add($"El nombre no puede superar los {NameMaxLength} caracteres.");

        if (trimmedEmail.Length == 0)
            errors.Add("El email es obligatorio.");
        else if (trimmedEmail.Length > EmailMaxLength)
            errors.Add($"El email no puede superar los {EmailMaxLength} caracteres.");
        else if (!IsValidEmail(trimmedEmail))
            errors.Add("El email no tiene un formato válido.");

        if (trimmedMessage.Length == 0)
            errors.Add("El mensaje es obligatorio.");
        else if (trimmedMessage.Length > MessageMaxLength)
            errors.Add($"El mensaje no puede superar los {MessageMaxLength} caracteres.");

        if (errors.Count > 0)
            throw new InvalidContactMessageException(errors);

        await mailer.SendContactMessageAsync(trimmedName, trimmedEmail, trimmedMessage, ct);
    }

    /// <summary>
    /// Formato de email pragmático: parseable por <see cref="MailAddress"/> y sin
    /// nombre para mostrar ni espacios (rechaza "Ana &lt;ana@x.com&gt;" y "a b@x.com").
    /// </summary>
    private static bool IsValidEmail(string email)
        => !email.Contains(' ')
           && MailAddress.TryCreate(email, out var parsed)
           && parsed.Address == email;
}
