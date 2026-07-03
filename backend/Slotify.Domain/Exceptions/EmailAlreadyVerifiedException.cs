namespace Slotify.Domain.Exceptions;

/// <summary>Se pidió reenviar la verificación pero el email ya está verificado.</summary>
public class EmailAlreadyVerifiedException()
    : Exception("Este email ya está verificado.");
