namespace Slotify.Domain.Exceptions;

/// <summary>El modo de reservas no es válido (solo 'online' | 'calendar_only'). HTTP 400.</summary>
public class InvalidBookingModeException(string mode)
    : Exception($"Modo de reservas inválido: '{mode}'. Usa 'online' o 'calendar_only'.");
