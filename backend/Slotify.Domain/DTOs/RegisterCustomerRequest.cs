namespace Slotify.Domain.DTOs;

/// <summary>
/// Alta de cliente (sin negocio) — API.md POST /auth/register. El teléfono es
/// opcional; si se indica, se usa para vincular reservas previas hechas como
/// invitado (sync invitado→usuario por blind index).
/// </summary>
public record RegisterCustomerRequest(string Email, string Password, string Name, string? Phone = null);
