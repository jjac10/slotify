using Slotify.Domain.Entities;

namespace Slotify.Domain.DTOs;

/// <summary>
/// Representación pública de un trabajador (para que el cliente elija con quién
/// reservar). No expone email/teléfono del staff.
/// </summary>
public record StaffResponse(Guid Id, Guid BusinessId, string Name, string Role, string Status)
{
    public static StaffResponse From(Staff s) => new(s.Id, s.BusinessId, s.Name, s.Role, s.Status);
}
