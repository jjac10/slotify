namespace Slotify.Domain.Exceptions;

/// <summary>La categoría no está en la lista admitida. HTTP 400.</summary>
public class InvalidCategoryException(string category)
    : Exception($"Categoría inválida: '{category}'.");
