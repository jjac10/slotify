namespace Slotify.Domain.DTOs;

/// <summary>Alta de cliente (sin negocio) — API.md POST /auth/register.</summary>
public record RegisterCustomerRequest(string Email, string Password, string Name);
