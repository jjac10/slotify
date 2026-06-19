namespace Slotify.Domain.DTOs;

/// <summary>
/// Fija la antelación mínima (en horas) con la que el cliente puede cancelar/reprogramar.
/// <c>0</c> = sin restricción. Solo el owner.
/// </summary>
public record SetCancellationCutoffRequest(int Hours);
