namespace Slotify.Domain.Exceptions;

/// <summary>No existe el negocio indicado.</summary>
public class BusinessNotFoundException(Guid businessId)
    : Exception($"No existe el negocio '{businessId}'.");
