namespace Slotify.Domain.DTOs;

/// <summary>Datos para crear un negocio (y su owner-staff asociado).</summary>
public record CreateBusinessRequest(Guid OwnerId, Guid TierId, string Name, string OwnerName);
