using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Slotify.Domain.DTOs;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Services;

namespace Slotify.API.Controllers;

/// <summary>
/// Lista de espera (solo usuarios registrados): apuntarse a un (servicio, día)
/// completo, ver las esperas propias y salir de la cola. El aviso al liberarse un
/// hueco lo dispara la cancelación de reservas.
/// </summary>
[ApiController]
[Authorize]
public class WaitlistController(WaitlistService waitlist) : ApiControllerBase
{
    /// <summary>Apuntarse a la cola de un (servicio, día) completo del negocio.</summary>
    [HttpPost("/businesses/{businessId:guid}/waitlist")]
    public async Task<ActionResult<WaitlistEntryResponse>> Join(
        Guid businessId, JoinWaitlistRequest request, CancellationToken ct)
    {
        var entry = await waitlist.JoinAsync(businessId, request.ServiceId, request.Date, CurrentUserId, ct);
        return StatusCode(StatusCodes.Status201Created, entry);
    }

    /// <summary>Esperas del usuario autenticado (próximas primero).</summary>
    [HttpGet("/me/waitlist")]
    public async Task<ActionResult<IReadOnlyList<WaitlistEntryResponse>>> ListMine(CancellationToken ct)
        => Ok(await waitlist.ListMineAsync(CurrentUserId, ct));

    /// <summary>Salir de la cola (solo el dueño de la entrada).</summary>
    [HttpDelete("/waitlist/{entryId:guid}")]
    public async Task<IActionResult> Leave(Guid entryId, CancellationToken ct)
    {
        await waitlist.LeaveAsync(entryId, CurrentUserId, ct);
        return NoContent();
    }
}
