namespace Slotify.Domain.DTOs;

/// <summary>Datos para registrar un propietario y su negocio (API.md POST /auth/register).</summary>
public record RegisterRequest(string Email, string Password, string Name, string BusinessName);
