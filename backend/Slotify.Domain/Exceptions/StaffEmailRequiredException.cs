namespace Slotify.Domain.Exceptions;

/// <summary>No se puede invitar a un trabajador sin email. HTTP 400.</summary>
public class StaffEmailRequiredException() : Exception("El trabajador necesita un email para poder invitarlo.");
