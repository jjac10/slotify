namespace Slotify.Domain.DTOs;

/// <summary>
/// Configuración de avisos del negocio: canales (email/WhatsApp) y antelación del
/// recordatorio en horas (0 = sin recordatorio). Solo el owner.
/// </summary>
public record SetNotificationSettingsRequest(bool NotifyByEmail, bool NotifyByWhatsapp, int ReminderHoursBefore);
