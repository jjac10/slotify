using Slotify.Domain.Entities;

namespace Slotify.Domain.DTOs;

/// <summary>Representación de un negocio para la API.</summary>
public record BusinessResponse(Guid Id, string Name, string Status, string ConfirmationMode)
{
    public static BusinessResponse From(Business b) => new(b.Id, b.Name, b.Status, b.ConfirmationMode);
}
