namespace Slotify.Domain.Exceptions;

/// <summary>Un invitado debe dar nombre + exactamente uno de teléfono/email. HTTP 400.</summary>
public class InvalidGuestContactException()
    : Exception("El invitado debe indicar nombre y exactamente uno de teléfono o email.");
