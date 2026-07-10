namespace Slotify.Domain.Exceptions;

/// <summary>Un campo del perfil público del negocio no es válido. HTTP 400.</summary>
public class InvalidBusinessProfileException(string message) : Exception(message);
