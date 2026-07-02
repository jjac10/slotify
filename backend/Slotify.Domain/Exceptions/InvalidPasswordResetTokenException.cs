namespace Slotify.Domain.Exceptions;

/// <summary>El token de recuperación no existe, está caducado o ya se usó.</summary>
public class InvalidPasswordResetTokenException()
    : Exception("El enlace de recuperación no es válido o ha caducado. Solicita uno nuevo.");
