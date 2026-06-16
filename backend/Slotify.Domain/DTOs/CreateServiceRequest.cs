namespace Slotify.Domain.DTOs;

/// <summary>Datos para crear un servicio (API.md POST /businesses/{id}/services).</summary>
public record CreateServiceRequest(
    string Name,
    string? Description,
    int DurationMinutes,
    decimal? Price,
    string? Color);
