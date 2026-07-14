using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Adaptador de <see cref="AvailabilityService"/> para la lista de espera: ¿queda algún
/// hueco libre ese día para ese servicio con CUALQUIER trabajador capaz de realizarlo?
/// </summary>
public class DayAvailabilityChecker(
    AvailabilityService availability,
    StaffService staff) : IDayAvailabilityChecker
{
    public async Task<bool> HasFreeSlotAsync(
        Guid businessId, Guid serviceId, DateOnly date, CancellationToken ct = default)
    {
        // Mismos trabajadores que ofrece el wizard para ese servicio.
        var candidates = await staff.ListAsync(businessId, serviceId, ct);
        foreach (var worker in candidates)
        {
            var slots = await availability.GetSlotsAsync(businessId, serviceId, worker.Id, date, DateTime.UtcNow, ct);
            if (slots.Count > 0)
                return true;
        }
        return false;
    }
}
