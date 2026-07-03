namespace Slotify.Domain.Exceptions;

/// <summary>El token de verificación de email no existe, ya se usó o ha caducado.</summary>
public class InvalidEmailVerificationTokenException()
    : Exception("El enlace de verificación no es válido o ha caducado.");
