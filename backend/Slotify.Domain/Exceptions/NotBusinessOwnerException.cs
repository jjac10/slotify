namespace Slotify.Domain.Exceptions;

/// <summary>El usuario no es el propietario del negocio (no autorizado).</summary>
public class NotBusinessOwnerException() : Exception("No tienes permisos sobre este negocio.");
