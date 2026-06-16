namespace Slotify.Domain.Exceptions;

/// <summary>El email ya está registrado (RF-AUTH-001: email único).</summary>
public class EmailAlreadyExistsException(string email)
    : Exception($"El email '{email}' ya está registrado.");
