namespace Slotify.Domain.Exceptions;

/// <summary>El mensaje del formulario de contacto/soporte no es válido. HTTP 400.</summary>
public class InvalidContactMessageException(IReadOnlyList<string> errors)
    : Exception("El mensaje de contacto no es válido: " + string.Join(" ", errors))
{
    /// <summary>Reglas incumplidas (para devolver detalle al cliente).</summary>
    public IReadOnlyList<string> Errors { get; } = errors;
}
