namespace Slotify.Domain.Exceptions;

/// <summary>La antelación mínima de cancelación debe estar entre 0 y 720 horas (30 días).</summary>
public class InvalidCancellationCutoffException(int hours)
    : Exception($"Antelación inválida: {hours} h (debe estar entre 0 y 720).");
