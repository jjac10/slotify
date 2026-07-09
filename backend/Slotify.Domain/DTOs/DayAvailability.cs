namespace Slotify.Domain.DTOs;

/// <summary>
/// Estado de un día del mes para el calendario de reserva:
/// 'closed' (sin horario o festivo de día completo), 'full' (abierto pero sin
/// huecos libres) o 'available' (quedan huecos).
/// </summary>
public record DayAvailability(DateOnly Date, string Status);
