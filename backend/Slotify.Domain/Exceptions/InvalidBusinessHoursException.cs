namespace Slotify.Domain.Exceptions;

/// <summary>Horario inválido (día fuera de 0–6, apertura≥cierre o días duplicados). HTTP 400.</summary>
public class InvalidBusinessHoursException(string message) : Exception(message);
