namespace Slotify.Domain.DTOs;

/// <summary>Credenciales de login (API.md POST /auth/login).</summary>
public record LoginRequest(string Email, string Password);
