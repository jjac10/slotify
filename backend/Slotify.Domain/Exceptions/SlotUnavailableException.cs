namespace Slotify.Domain.Exceptions;

/// <summary>El slot ya no está disponible (solapa con otra reserva del staff). HTTP 409.</summary>
public class SlotUnavailableException() : Exception("El horario ya no está disponible para ese trabajador.");
