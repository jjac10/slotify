namespace Slotify.Domain.Exceptions;

/// <summary>No existe el trabajador (o no pertenece al negocio). HTTP 404.</summary>
public class StaffNotFoundException(Guid staffId)
    : Exception($"No existe el trabajador '{staffId}' en este negocio.");
