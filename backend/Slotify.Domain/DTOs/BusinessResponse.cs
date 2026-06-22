using Slotify.Domain.Entities;

namespace Slotify.Domain.DTOs;

/// <summary>
/// Representación de un negocio para la API. <c>Plan</c> es el código del tier
/// ('free'|'premium'); puede venir null si la consulta no cargó el tier.
/// </summary>
public record BusinessResponse(
    Guid Id, string Name, string Status, string ConfirmationMode, int CancellationCutoffHours, string? Plan,
    string? Category, string? PhotoUrl, double? Latitude, double? Longitude,
    double? Rating, int ReviewCount, string BookingMode,
    bool NotifyByEmail, bool NotifyByWhatsapp, int ReminderHoursBefore)
{
    public static BusinessResponse From(Business b) =>
        new(b.Id, b.Name, b.Status, b.ConfirmationMode, b.CancellationCutoffHours, b.Tier?.Code,
            b.Category, b.PhotoUrl, b.Latitude, b.Longitude, b.Rating, b.ReviewCount, b.BookingMode,
            b.NotifyByEmail, b.NotifyByWhatsapp, b.ReminderHoursBefore);

    /// <summary>Con el código de plan explícito (p. ej. tras cambiar el tier, cuando la nav puede estar obsoleta).</summary>
    public static BusinessResponse From(Business b, string planCode) =>
        new(b.Id, b.Name, b.Status, b.ConfirmationMode, b.CancellationCutoffHours, planCode,
            b.Category, b.PhotoUrl, b.Latitude, b.Longitude, b.Rating, b.ReviewCount, b.BookingMode,
            b.NotifyByEmail, b.NotifyByWhatsapp, b.ReminderHoursBefore);
}
