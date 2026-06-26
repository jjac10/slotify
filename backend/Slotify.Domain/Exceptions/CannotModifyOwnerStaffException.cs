namespace Slotify.Domain.Exceptions;

/// <summary>No se puede dar de baja al trabajador con rol 'owner' del negocio. HTTP 409.</summary>
public class CannotModifyOwnerStaffException()
    : Exception("No se puede dar de baja al propietario del equipo del negocio.");
