namespace Slotify.Domain.Exceptions;

/// <summary>
/// La confirmación de borrado no coincide con el nombre exacto del negocio
/// (confirmación máxima: hay que escribirlo tal cual). HTTP 400.
/// </summary>
public class BusinessNameMismatchException()
    : Exception("El nombre escrito no coincide con el nombre exacto del negocio.");
