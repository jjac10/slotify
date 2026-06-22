namespace Slotify.Domain.Exceptions;

/// <summary>La configuración de avisos no es válida (antelación del recordatorio fuera de rango). HTTP 400.</summary>
public class InvalidNotificationSettingsException(string message) : Exception(message);
