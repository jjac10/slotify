namespace Slotify.Domain.Exceptions;

/// <summary>page/pageSize fuera de rango en un listado paginado. HTTP 400.</summary>
public class InvalidPaginationException(int page, int pageSize, int maxPageSize)
    : Exception($"Paginación inválida: 'page' debe ser >= 1 y 'pageSize' estar entre 1 y {maxPageSize} (recibido page={page}, pageSize={pageSize}).");
