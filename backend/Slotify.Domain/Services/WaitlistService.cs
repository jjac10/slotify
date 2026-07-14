using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Lista de espera por (servicio, día): un usuario registrado se apunta SOLO cuando el
/// día está completo (si quedan huecos → se reserva, no se hace cola), sin duplicarse y
/// con posición incremental. Cuando una cancelación libera hueco, se avisa al primero de
/// la cola (waiting → notified) por los canales del negocio; el aviso es best-effort y
/// la cola avanza igualmente. En esta versión no entran invitados (el esquema lo deja
/// preparado); el siguiente de la cola se avisa con la próxima cancelación.
/// </summary>
public class WaitlistService(
    IWaitlistRepository waitlists,
    IServiceRepository services,
    IDayAvailabilityChecker availability,
    IBusinessRepository businesses,
    NotificationService notifications)
{
    public async Task<WaitlistEntryResponse> JoinAsync(
        Guid businessId, Guid serviceId, DateOnly date, Guid userId, CancellationToken ct = default)
    {
        var service = await services.GetByIdAsync(serviceId, ct);
        if (service is null || service.BusinessId != businessId)
            throw new ServiceNotFoundException(serviceId);

        if (date < DateOnly.FromDateTime(DateTime.UtcNow))
            throw new InvalidWaitlistDateException();

        if (await waitlists.ExistsForUserAsync(serviceId, date, userId, ct))
            throw new AlreadyOnWaitlistException();

        // Si aún queda hueco con cualquier trabajador, no hay cola que valga.
        if (await availability.HasFreeSlotAsync(businessId, serviceId, date, ct))
            throw new WaitlistNotNeededException();

        var entry = new WaitlistEntry
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            ServiceId = serviceId,
            Date = date,
            UserId = userId,
            Position = await waitlists.CountWaitingAsync(serviceId, date, ct) + 1,
            Status = "waiting",
            CreatedAt = DateTime.UtcNow,
        };
        await waitlists.AddAsync(entry, ct);

        entry.Service = service;
        return WaitlistEntryResponse.From(entry);
    }

    /// <summary>Entradas de lista de espera del usuario (para "Mi Slotify").</summary>
    public async Task<IReadOnlyList<WaitlistEntryResponse>> ListMineAsync(Guid userId, CancellationToken ct = default)
    {
        var list = await waitlists.ListByUserAsync(userId, ct);
        return list.Select(WaitlistEntryResponse.From).ToList();
    }

    /// <summary>Salir de la cola. Solo el dueño de la entrada (para otros: como si no existiera).</summary>
    public async Task LeaveAsync(Guid entryId, Guid userId, CancellationToken ct = default)
    {
        var entry = await waitlists.GetByIdAsync(entryId, ct);
        if (entry is null || entry.UserId != userId)
            throw new WaitlistEntryNotFoundException(entryId);

        await waitlists.DeleteAsync(entryId, ct);
    }

    /// <summary>
    /// Se liberó un hueco (cancelación): convierte el inicio UTC al día local del negocio
    /// y avisa al primero de la cola de ese (servicio, día). Best-effort: nunca rompe la
    /// cancelación que lo origina.
    /// </summary>
    public async Task NotifySlotFreedAsync(
        Guid businessId, Guid serviceId, DateTime startTimeUtc, Guid reservationId, CancellationToken ct = default)
    {
        try
        {
            var business = await businesses.GetByIdAsync(businessId, ct);
            var tz = TimeZoneInfo.FindSystemTimeZoneById(business?.Timezone ?? "Europe/Madrid");
            var localDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(startTimeUtc, tz));
            await NotifyNextAsync(serviceId, localDay, reservationId, ct);
        }
        catch
        {
            // Best-effort: un fallo avisando a la cola jamás debe tumbar la cancelación.
        }
    }

    /// <summary>Avisa al primero en cola (waiting → notified) de un (servicio, día).</summary>
    public async Task NotifyNextAsync(
        Guid serviceId, DateOnly date, Guid reservationId, CancellationToken ct = default)
    {
        var entry = await waitlists.FirstWaitingAsync(serviceId, date, ct);
        if (entry is null)
            return;

        entry.Status = "notified";
        entry.NotifiedAt = DateTime.UtcNow;
        await waitlists.UpdateAsync(entry, ct);

        // El aviso sale por los canales del negocio (email/WhatsApp); si falla, la cola
        // ya avanzó — DispatchEventAsync es best-effort por diseño.
        var startOfDayUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        await notifications.DispatchEventAsync(
            new NotificationContext(entry.BusinessId, reservationId, entry.UserId, entry.GuestId, startOfDayUtc),
            "waitlist_slot_freed", ct);
    }
}
