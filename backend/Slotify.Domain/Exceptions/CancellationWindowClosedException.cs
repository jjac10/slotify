namespace Slotify.Domain.Exceptions;

/// <summary>
/// El cliente intenta cancelar/reprogramar dentro de la ventana de antelación mínima
/// que fija el negocio (p. ej. menos de 24 h antes). El owner/staff no está sujeto a ella.
/// </summary>
public class CancellationWindowClosedException(int cutoffHours)
    : Exception($"No se puede cancelar ni reprogramar con menos de {cutoffHours} h de antelación.");
