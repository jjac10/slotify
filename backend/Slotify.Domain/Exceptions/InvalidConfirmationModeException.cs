namespace Slotify.Domain.Exceptions;

/// <summary>El modo de confirmación debe ser 'auto' o 'manual'.</summary>
public class InvalidConfirmationModeException(string mode)
    : Exception($"Modo de confirmación inválido: '{mode}' (debe ser 'auto' o 'manual').");
