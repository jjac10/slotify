using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Slotify.Domain.Interfaces;

namespace Slotify.API.Realtime;

/// <summary>
/// Hub de tiempo real (/hubs/reservations). Cada usuario autenticado queda suscrito a
/// su canal personal (<c>user:{id}</c>) al conectar; el owner/staff puede unirse además
/// al grupo de su negocio (<see cref="JoinBusiness"/>) para que la Agenda/Panel se
/// refresquen solos. El hub solo emite ids y tipo de evento — nunca datos personales.
/// </summary>
[Authorize]
public class ReservationsHub(IBusinessRepository businesses, IStaffRepository staff) : Hub
{
    public static string UserGroup(Guid userId) => $"user:{userId}";

    public static string BusinessGroup(Guid businessId) => $"business:{businessId}";

    public override async Task OnConnectedAsync()
    {
        // El claim 'sub' del JWT (MapInboundClaims=false lo conserva tal cual).
        if (Context.UserIdentifier is null && Context.User?.FindFirst("sub")?.Value is { } sub)
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(Guid.Parse(sub)));
        else if (Context.UserIdentifier is { } id)
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(Guid.Parse(id)));

        await base.OnConnectedAsync();
    }

    /// <summary>Une la conexión al grupo del negocio. Solo su owner o su staff.</summary>
    public async Task JoinBusiness(Guid businessId)
    {
        var sub = Context.User?.FindFirst("sub")?.Value
            ?? throw new HubException("Sin identidad.");
        var userId = Guid.Parse(sub);

        var business = await businesses.GetByIdAsync(businessId);
        var isOwner = business is not null && business.OwnerId == userId;
        var isStaff = !isOwner && await staff.ExistsForUserAsync(userId, businessId);
        if (!isOwner && !isStaff)
            throw new HubException("Solo el owner o el staff del negocio pueden unirse a su canal.");

        await Groups.AddToGroupAsync(Context.ConnectionId, BusinessGroup(businessId));
    }
}
