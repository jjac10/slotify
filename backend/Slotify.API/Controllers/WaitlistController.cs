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
        try
        {
            var entry = await waitlist.JoinAsync(businessId, request.ServiceId, request.Date, CurrentUserId, ct);
            return StatusCode(StatusCodes.Status201Created, entry);
        }
        catch (ServiceNotFoundException ex)
        {
            return NotFound(new { error = "service_not_found", message = ex.Message });
        }
        catch (InvalidWaitlistDateException ex)
        {
            return BadRequest(new { error = "invalid_date", message = ex.Message });
        }
        catch (AlreadyOnWaitlistException ex)
        {
            return Conflict(new { error = "already_waiting", message = ex.Message });
        }
        catch (WaitlistNotNeededException ex)
        {
            return Conflict(new { error = "slots_available", message = ex.Message });
        }
    }

    /// <summary>Esperas del usuario autenticado (próximas primero).</summary>
    [HttpGet("/me/waitlist")]
    public async Task<ActionResult<IReadOnlyList<WaitlistEntryResponse>>> ListMine(CancellationToken ct)
        => Ok(await waitlist.ListMineAsync(CurrentUserId, ct));

    /// <summary>Salir de la cola (solo el dueño de la entrada).</summary>
    [HttpDelete("/waitlist/{entryId:guid}")]
    public async Task<IActionResult> Leave(Guid entryId, CancellationToken ct)
    {
        try
        {
            await waitlist.LeaveAsync(entryId, CurrentUserId, ct);
            return NoContent();
        }
        catch (WaitlistEntryNotFoundException ex)
        {
            return NotFound(new { error = "waitlist_entry_not_found", message = ex.Message });
        }
    }
}
