namespace Slotify.Domain.DTOs;

/// <summary>Un hueco reservable (UTC). El cliente usa <see cref="Start"/> como startTime.</summary>
public record AvailableSlot(DateTime Start, DateTime End);
