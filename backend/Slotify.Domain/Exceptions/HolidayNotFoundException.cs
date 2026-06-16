namespace Slotify.Domain.Exceptions;

/// <summary>No existe el festivo indicado. HTTP 404.</summary>
public class HolidayNotFoundException(Guid holidayId)
    : Exception($"No existe el festivo '{holidayId}'.");
