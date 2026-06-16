using Slotify.Domain.Entities;

namespace Slotify.Domain.DTOs;

/// <summary>Representación de un servicio para la API.</summary>
public record ServiceResponse(
    Guid Id,
    Guid BusinessId,
    string Name,
    string? Description,
    int DurationMinutes,
    decimal? Price,
    string? Color,
    string Status)
{
    public static ServiceResponse From(Service s) =>
        new(s.Id, s.BusinessId, s.Name, s.Description, s.DurationMinutes, s.Price, s.Color, s.Status);
}
