namespace Slotify.Domain.DTOs;

/// <summary>POST /support/contact — formulario público de contacto/soporte.</summary>
public record ContactSupportRequest(string? Name, string? Email, string? Message);
