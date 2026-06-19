namespace Slotify.Domain.DTOs;

/// <summary>Datos para editar un trabajador existente (nombre/contacto).</summary>
public record UpdateStaffRequest(string Name, string? Email, string? Phone);
