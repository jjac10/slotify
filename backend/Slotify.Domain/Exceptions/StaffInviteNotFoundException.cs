namespace Slotify.Domain.Exceptions;

/// <summary>El token de invitación no existe o ya se usó. HTTP 404.</summary>
public class StaffInviteNotFoundException() : Exception("La invitación no es válida o ya se usó.");
