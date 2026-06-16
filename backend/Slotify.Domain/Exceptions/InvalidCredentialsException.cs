namespace Slotify.Domain.Exceptions;

/// <summary>Email o contraseña incorrectos (RF-AUTH-002).</summary>
public class InvalidCredentialsException() : Exception("Email o contraseña incorrectos.");
