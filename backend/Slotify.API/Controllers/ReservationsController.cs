using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Slotify.Domain.DTOs;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Services;

namespace Slotify.API.Controllers;

[ApiController]
[Route("reservations")]
public class ReservationsController(
    BookingService booking,
    ReservationManagementService management,
    GuestReservationLookupService guestLookup) : ApiControllerBase
{
    /// <summary>Crea una reserva (invitado o usuario logueado).</summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<ReservationResponse>> Create(CreateReservationRequest request, CancellationToken ct)
    {
        // Si la petición trae un JWT válido, el cliente es ese usuario; si no, es invitado.
        Guid? userId = User.FindFirstValue("sub") is { } sub ? Guid.Parse(sub) : null;

        try
        {
            var result = await booking.CreateAsync(request, userId, ct);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
        }
        catch (ServiceNotFoundException ex)
        {
            return NotFound(new { error = "service_not_found", message = ex.Message });
        }
        catch (StaffNotFoundException ex)
        {
            return NotFound(new { error = "staff_not_found", message = ex.Message });
        }
        catch (InvalidGuestContactException ex)
        {
            return BadRequest(new { error = "invalid_guest_contact", message = ex.Message });
        }
        catch (SelfBookingNotAllowedException ex)
        {
            return BadRequest(new { error = "self_booking_not_allowed", message = ex.Message });
        }
        catch (SlotUnavailableException ex)
        {
            return Conflict(new { error = "slot_unavailable", message = ex.Message });
        }
        catch (FreemiumLimitReachedException ex)
        {
            return Conflict(new { error = "limit_reached", message = ex.Message });
        }
    }

    /// <summary>Obtiene una reserva por id.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ReservationResponse>> Get(Guid id, CancellationToken ct)
    {
        var reservation = await booking.GetAsync(id, ct);
        return reservation is null ? NotFound() : Ok(reservation);
    }

    /// <summary>Reservas del usuario autenticado ("mis reservas").</summary>
    [HttpGet("mine")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<ReservationResponse>>> ListMine(CancellationToken ct)
        => Ok(await management.ListMineAsync(CurrentUserId, ct));

    /// <summary>Reservas de un invitado por su teléfono o email (sin cuenta). Público.</summary>
    [HttpGet("lookup")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ReservationResponse>>> Lookup([FromQuery] string? contact, CancellationToken ct)
        => Ok(await guestLookup.LookupAsync(contact, ct));

    /// <summary>Agenda del negocio (owner o staff). Filtros opcionales por fecha y trabajador.</summary>
    [HttpGet("/businesses/{businessId:guid}/reservations")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<ReservationResponse>>> ListForBusiness(
        Guid businessId, [FromQuery] DateOnly? date, [FromQuery] Guid? staffId, CancellationToken ct)
    {
        try
        {
            return Ok(await management.ListForBusinessAsync(businessId, CurrentUserId, date, staffId, ct));
        }
        catch (ReservationForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message });
        }
    }

    /// <summary>
    /// Reprograma una reserva. Con JWT: owner, staff o el propio usuario. Sin JWT: el
    /// invitado dueño de la reserva, verificado con su teléfono/email en <c>contact</c>.
    /// Conserva la duración + auditoría; respeta la ventana de antelación del negocio.
    /// </summary>
    [HttpPatch("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ReservationResponse>> Reschedule(Guid id, RescheduleReservationRequest request, CancellationToken ct)
    {
        try
        {
            var userId = TryGetUserId();
            var result = userId is { } uid
                ? await management.RescheduleAsync(id, uid, request.StartTime, ct)
                : await management.RescheduleAsGuestAsync(id, request.Contact, request.StartTime, ct);
            return Ok(result);
        }
        catch (ReservationNotFoundException ex)
        {
            return NotFound(new { error = "reservation_not_found", message = ex.Message });
        }
        catch (ReservationForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message });
        }
        catch (CancellationWindowClosedException ex)
        {
            return Conflict(new { error = "window_closed", message = ex.Message });
        }
        catch (SlotUnavailableException ex)
        {
            return Conflict(new { error = "slot_unavailable", message = ex.Message });
        }
        catch (ReservationConcurrencyException ex)
        {
            return Conflict(new { error = "concurrency_conflict", message = ex.Message });
        }
    }

    /// <summary>Confirma una reserva pendiente (owner del negocio o su staff). Negocios con confirmación manual.</summary>
    [HttpPost("{id:guid}/confirm")]
    [Authorize]
    public async Task<ActionResult<ReservationResponse>> Confirm(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await management.ConfirmAsync(id, CurrentUserId, ct));
        }
        catch (ReservationNotFoundException ex)
        {
            return NotFound(new { error = "reservation_not_found", message = ex.Message });
        }
        catch (ReservationForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message });
        }
        catch (ReservationNotPendingException ex)
        {
            return Conflict(new { error = "not_pending", message = ex.Message });
        }
    }

    /// <summary>
    /// Cancela una reserva (hard-delete + auditoría). Con JWT: owner, staff o el propio
    /// usuario. Sin JWT: el invitado dueño, verificado con su teléfono/email en <c>contact</c>.
    /// Respeta la ventana de antelación del negocio.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Cancel(Guid id, [FromQuery] string? reason, [FromQuery] string? contact, CancellationToken ct)
    {
        try
        {
            var userId = TryGetUserId();
            if (userId is { } uid)
                await management.CancelAsync(id, uid, reason, ct);
            else
                await management.CancelAsGuestAsync(id, contact, reason, ct);
            return NoContent();
        }
        catch (ReservationNotFoundException ex)
        {
            return NotFound(new { error = "reservation_not_found", message = ex.Message });
        }
        catch (ReservationForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message });
        }
        catch (CancellationWindowClosedException ex)
        {
            return Conflict(new { error = "window_closed", message = ex.Message });
        }
    }
}
