namespace Slotify.Domain.Exceptions;

/// <summary>El trabajador ya tiene cuenta enlazada; no se puede volver a invitar. HTTP 409.</summary>
public class StaffAlreadyHasAccountException() : Exception("Este trabajador ya tiene una cuenta.");
