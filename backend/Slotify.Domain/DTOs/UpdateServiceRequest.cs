namespace Slotify.Domain.DTOs;

/// <summary>Datos para editar un servicio (reemplaza sus campos). Solo el owner.</summary>
public record UpdateServiceRequest(
    string Name,
    string? Description,
    int DurationMinutes,
    decimal? Price,
    string? Color);
