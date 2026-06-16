namespace Slotify.Domain.Exceptions;

/// <summary>No existe el servicio (o no pertenece al negocio). HTTP 404.</summary>
public class ServiceNotFoundException(Guid serviceId)
    : Exception($"No existe el servicio '{serviceId}' en este negocio.");
