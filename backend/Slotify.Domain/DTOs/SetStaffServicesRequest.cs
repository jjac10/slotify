namespace Slotify.Domain.DTOs;

/// <summary>
/// Fija qué servicios puede realizar un trabajador (reemplaza la lista completa).
/// Lista vacía = el trabajador puede realizar todos los servicios. Solo el owner.
/// </summary>
public record SetStaffServicesRequest(IReadOnlyList<Guid> ServiceIds);
