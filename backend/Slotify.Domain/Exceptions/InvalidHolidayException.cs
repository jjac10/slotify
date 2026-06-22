namespace Slotify.Domain.Exceptions;

/// <summary>Datos de festivo inválidos (rango o franja horaria). HTTP 400.</summary>
public class InvalidHolidayException(string message) : Exception(message);
