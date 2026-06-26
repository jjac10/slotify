namespace Slotify.Domain.DTOs;

/// <summary>Datos para dar de alta un trabajador (empleado) en un negocio.</summary>
public record CreateStaffRequest(string Name, string? Email, string? Phone);
