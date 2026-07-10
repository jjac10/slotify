using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Slotify.Domain.DTOs;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Services;

namespace Slotify.API.Controllers;

[ApiController]
[Route("reservations")]
public class ReservationsController(
    BookingService booking,
    ReservationManagementService management,
    GuestReservationLookupService guestLookup,
    GuestOtpService guestOtp,
    NotificationService notifications) : ApiControllerBase
{
    /// <summary>Respuesta estándar cuando el código OTP de invitado falta o no es válido.</summary>
    private ObjectResult InvalidOtp() => StatusCode(StatusCodes.Status403Forbidden,
        new { error = "invalid_otp", message = "El código de verificación no es válido o ha caducado. Pide uno nuevo." });

    /// <summary>Construye el contexto de notificación a partir de una reserva.</summary>
    private static NotificationContext Ctx(ReservationResponse r) =>
        new(r.BusinessId, r.Id, r.UserId, r.GuestId, r.StartTime);

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
            await notifications.DispatchEventAsync(Ctx(result), "created", ct);
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
        catch (OnlineBookingDisabledException ex)
        {
            return Conflict(new { error = "online_booking_disabled", message = ex.Message });
        }
        catch (ContactBelongsToAccountException ex)
        {
            return Conflict(new { error = "contact_belongs_to_account", message = ex.Message });
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

    /// <summary>
    /// Reservas del usuario autenticado ("mis reservas"). <c>?scope=</c> acota por inicio:
    /// <c>upcoming</c> (próximas, ascendente), <c>past</c> (pasadas, la más reciente primero)
    /// o <c>all</c> (default). Paginado en BD con <c>?page=</c> (1-based, default 1) y
    /// <c>?pageSize=</c> (default 20, máx. 50); responde <c>{ items, total, page, pageSize }</c>.
    /// </summary>
    [HttpGet("mine")]
    [Authorize]
    public async Task<ActionResult<PagedResponse<ReservationResponse>>> ListMine(
        [FromQuery] string? scope,
        [FromQuery] int page = 1, [FromQuery] int pageSize = ReservationManagementService.DefaultPageSize,
        CancellationToken ct = default)
    {
        if (!TryParseScope(scope, out var parsedScope))
            return BadRequest(new { error = "invalid_scope", message = "scope debe ser 'upcoming', 'past' o 'all'." });

        try
        {
            return Ok(await management.ListMineAsync(CurrentUserId, parsedScope, page, pageSize, ct));
        }
        catch (InvalidPaginationException ex)
        {
            return BadRequest(new { error = "invalid_pagination", message = ex.Message });
        }
    }

    /// <summary>Mapea <c>?scope=</c> (case-insensitive; vacío → All) a <see cref="ReservationScope"/>.</summary>
    private static bool TryParseScope(string? scope, out ReservationScope parsed)
    {
        (var ok, parsed) = scope?.ToLowerInvariant() switch
        {
            null or "" or "all" => (true, ReservationScope.All),
            "upcoming" => (true, ReservationScope.Upcoming),
            "past" => (true, ReservationScope.Past),
            _ => (false, ReservationScope.All),
        };
        return ok;
    }

    /// <summary>
    /// Pide el código de verificación (OTP) del invitado: se envía por email o al teléfono
    /// según el contacto. Responde 204 SIEMPRE (anti-enumeración) y va rate-limited.
    /// </summary>
    [HttpPost("lookup/otp")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RequestLookupOtp(RequestGuestOtpRequest request, CancellationToken ct)
    {
        await guestOtp.RequestCodeAsync(request.Contact, ct);
        return NoContent();
    }

    /// <summary>
    /// Reservas de un invitado por su teléfono o email (sin cuenta). Público. El contacto
    /// va en el body (POST) para no exponer datos personales en la URL/logs. Requiere el
    /// código OTP pedido antes en POST /reservations/lookup/otp (verificación de identidad).
    /// </summary>
    [HttpPost("lookup")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<IReadOnlyList<ReservationResponse>>> Lookup(LookupGuestReservationsRequest request, CancellationToken ct)
    {
        if (!await guestOtp.VerifyAsync(request.Contact, request.OtpCode, ct))
            return InvalidOtp();

        return Ok(await guestLookup.LookupAsync(request.Contact, ct));
    }

    /// <summary>
    /// Agenda del negocio (owner o staff). Filtros opcionales por fecha y trabajador.
    /// Paginado en BD con <c>?page=</c> (1-based, default 1) y <c>?pageSize=</c> (default 20,
    /// máx. 50); responde <c>{ items, total, page, pageSize }</c>.
    /// </summary>
    [HttpGet("/businesses/{businessId:guid}/reservations")]
    [Authorize]
    public async Task<ActionResult<PagedResponse<ReservationResponse>>> ListForBusiness(
        Guid businessId, [FromQuery] DateOnly? date, [FromQuery] Guid? staffId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = ReservationManagementService.DefaultPageSize,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await management.ListForBusinessAsync(businessId, CurrentUserId, date, staffId, page, pageSize, ct));
        }
        catch (InvalidPaginationException ex)
        {
            return BadRequest(new { error = "invalid_pagination", message = ex.Message });
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
            // Invitado (sin JWT): además del contacto, exige el código OTP vigente.
            if (userId is null && !await guestOtp.VerifyAsync(request.Contact, request.OtpCode, ct))
                return InvalidOtp();

            var result = userId is { } uid
                ? await management.RescheduleAsync(id, uid, request.StartTime, ct)
                : await management.RescheduleAsGuestAsync(id, request.Contact, request.StartTime, ct);
            await notifications.DispatchEventAsync(Ctx(result), "rescheduled", ct);
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
            var result = await management.ConfirmAsync(id, CurrentUserId, ct);
            await notifications.DispatchEventAsync(Ctx(result), "confirmed", ct);
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
        catch (ReservationNotPendingException ex)
        {
            return Conflict(new { error = "not_pending", message = ex.Message });
        }
    }

    /// <summary>
    /// Cancela una reserva (hard-delete + auditoría). Con JWT: owner, staff o el propio
    /// usuario. Sin JWT: el invitado dueño, verificado con su teléfono/email en <c>contact</c>.
    /// El motivo y el contacto van en el body (no en la URL) por ser datos personales.
    /// Respeta la ventana de antelación del negocio.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [AllowAnonymous]
    public async Task<IActionResult> Cancel(Guid id, CancelReservationRequest request, CancellationToken ct)
    {
        try
        {
            // Capturamos los datos antes de cancelar (la cancelación hace hard-delete).
            var snapshot = await booking.GetAsync(id, ct);

            var userId = TryGetUserId();
            // Invitado (sin JWT): además del contacto, exige el código OTP vigente.
            if (userId is null && !await guestOtp.VerifyAsync(request.Contact, request.OtpCode, ct))
                return InvalidOtp();

            if (userId is { } uid)
                await management.CancelAsync(id, uid, request.Reason, ct);
            else
                await management.CancelAsGuestAsync(id, request.Contact, request.Reason, ct);

            if (snapshot is not null)
                await notifications.DispatchEventAsync(Ctx(snapshot), "cancelled", ct);
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
