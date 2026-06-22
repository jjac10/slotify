namespace Slotify.Domain.DTOs;

/// <summary>
/// Datos del perfil público del negocio (Explorar). Todos opcionales; null deja el
/// campo sin valor. <c>Category</c> debe ser un código válido (BusinessCategories).
/// </summary>
public record UpdateBusinessProfileRequest(
    string? Category,
    string? PhotoUrl,
    double? Latitude,
    double? Longitude);
