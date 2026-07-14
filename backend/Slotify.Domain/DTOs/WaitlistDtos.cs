using Slotify.Domain.Entities;

namespace Slotify.Domain.DTOs;

/// <summary>POST /businesses/{id}/waitlist — apuntarse a la cola de un (servicio, día).</summary>
public record JoinWaitlistRequest(Guid ServiceId, DateOnly Date);

/// <summary>Entrada de lista de espera del usuario (GET /me/waitlist).</summary>
public record WaitlistEntryResponse(
    Guid Id, Guid BusinessId, string? BusinessName, Guid ServiceId, string? ServiceName,
    DateOnly Date, int Position, string Status)
{
    public static WaitlistEntryResponse From(WaitlistEntry e) =>
        new(e.Id, e.BusinessId, e.Business?.Name, e.ServiceId, e.Service?.Name,
            e.Date, e.Position, e.Status);
}
