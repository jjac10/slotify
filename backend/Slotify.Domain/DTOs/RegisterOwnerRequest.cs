namespace Slotify.Domain.DTOs;

/// <summary>Alta de propietario + su negocio (API.md POST /auth/register-owner).</summary>
public record RegisterOwnerRequest(string Email, string Password, string Name, string BusinessName);
