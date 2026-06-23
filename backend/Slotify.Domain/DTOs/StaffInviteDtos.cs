namespace Slotify.Domain.DTOs;

/// <summary>Resultado de invitar a un empleado: su email y el token para construir el enlace.</summary>
public record StaffInviteResponse(Guid StaffId, string Email, string Token);

/// <summary>Datos de una invitación (para la pantalla de "aceptar invitación"), sin exponer nada sensible.</summary>
public record StaffInviteInfoResponse(string BusinessName, string StaffName, string Email);

/// <summary>El empleado fija su contraseña para crear su cuenta a partir del token de invitación.</summary>
public record AcceptStaffInviteRequest(string Password);
