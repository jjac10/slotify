namespace Slotify.Domain.Exceptions;

/// <summary>La foto subida no es válida (tipo no permitido o demasiado grande). HTTP 400.</summary>
public class InvalidPhotoException(string message) : Exception(message);
