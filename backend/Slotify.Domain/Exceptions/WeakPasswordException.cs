namespace Slotify.Domain.Exceptions;

/// <summary>La contraseña no cumple la política de seguridad (RF-AUTH-001).</summary>
public class WeakPasswordException(IReadOnlyList<string> errors)
    : Exception("La contraseña no es segura: " + string.Join(" ", errors))
{
    /// <summary>Reglas incumplidas (para devolver detalle al cliente).</summary>
    public IReadOnlyList<string> Errors { get; } = errors;
}
