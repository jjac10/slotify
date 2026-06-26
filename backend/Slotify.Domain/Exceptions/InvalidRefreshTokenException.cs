namespace Slotify.Domain.Exceptions;

/// <summary>El refresh token no existe, ya se usó o ha caducado (RF-AUTH-002).</summary>
public class InvalidRefreshTokenException() : Exception("El refresh token no es válido o ha caducado.");
