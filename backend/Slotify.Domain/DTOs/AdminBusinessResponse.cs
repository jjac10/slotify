namespace Slotify.Domain.DTOs;

/// <summary>Fila del directorio de moderación del admin (GET /admin/businesses).</summary>
public record AdminBusinessResponse(Guid Id, string Name, string OwnerEmail, DateTime CreatedAt);

/// <summary>
/// Confirmación máxima del borrado de un negocio por su owner
/// (POST /businesses/{id}/delete): nombre exacto + contraseña actual.
/// </summary>
public record DeleteBusinessRequest(string? Name, string? Password);
