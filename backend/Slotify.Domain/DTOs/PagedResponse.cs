namespace Slotify.Domain.DTOs;

/// <summary>
/// Envoltorio estándar de respuestas paginadas de la API:
/// <c>{ items, total, page, pageSize }</c>. <c>Total</c> es el número de
/// resultados que cumplen el filtro (no los de la página).
/// </summary>
public record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
