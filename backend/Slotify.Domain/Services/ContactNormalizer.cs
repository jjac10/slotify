namespace Slotify.Domain.Services;

/// <summary>
/// Normaliza teléfono/email antes de calcular el blind index (ADR #5), para que
/// el hash coincida siempre. Nota: la normalización E.164 completa del teléfono
/// es una mejora futura; aquí se hace una limpieza básica.
/// </summary>
public static class ContactNormalizer
{
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static string NormalizePhone(string phone) =>
        new string(phone.Where(c => !char.IsWhiteSpace(c)).ToArray());
}
