using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Slotify.Domain.DTOs;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Services;

namespace Slotify.API.Controllers;

[ApiController]
[Route("businesses")]
public class BusinessesController(
    BusinessService businesses,
    BusinessDeletionService businessDeletion,
    SubscriptionService subscriptions) : ApiControllerBase
{
    /// <summary>Lista los negocios del owner autenticado.</summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<BusinessResponse>>> ListMine(CancellationToken ct)
        => Ok(await businesses.ListByOwnerAsync(CurrentUserId, ct));

    /// <summary>
    /// Elimina el negocio y TODOS sus datos en cascada (RGPD). Confirmación máxima:
    /// el owner debe escribir el nombre exacto del negocio y su contraseña actual.
    /// Bloqueado si hay reservas futuras activas (409): primero hay que cancelarlas.
    /// </summary>
    [HttpPost("{id:guid}/delete")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id, DeleteBusinessRequest request, CancellationToken ct)
    {
        try
        {
            await businessDeletion.DeleteAsOwnerAsync(id, CurrentUserId, request.Name, request.Password, ct);
            return NoContent();
        }
        catch (BusinessNotFoundException ex)
        {
            return NotFound(new { error = "business_not_found", message = ex.Message });
        }
        catch (InvalidCredentialsException)
        {
            return BadRequest(new { error = "invalid_password", message = "La contraseña no es correcta." });
        }
        catch (NotBusinessOwnerException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message });
        }
        catch (BusinessNameMismatchException ex)
        {
            return BadRequest(new { error = "name_mismatch", message = ex.Message });
        }
        catch (BusinessHasFutureReservationsException ex)
        {
            return Conflict(new { error = "business_has_future_reservations", message = ex.Message });
        }
    }

    /// <summary>Cambia el modo de confirmación de reservas del negocio ('auto'|'manual'). Solo el owner.</summary>
    [HttpPut("{id:guid}/confirmation-mode")]
    [Authorize]
    public async Task<ActionResult<BusinessResponse>> SetConfirmationMode(
        Guid id, SetConfirmationModeRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await businesses.SetConfirmationModeAsync(id, CurrentUserId, request.Mode, ct));
        }
        catch (InvalidConfirmationModeException ex)
        {
            return BadRequest(new { error = "invalid_confirmation_mode", message = ex.Message });
        }
        catch (BusinessNotFoundException ex)
        {
            return NotFound(new { error = "business_not_found", message = ex.Message });
        }
        catch (NotBusinessOwnerException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message });
        }
    }

    /// <summary>Fija la antelación mínima (horas) para que el cliente cancele/reprograme. 0 = sin límite. Solo el owner.</summary>
    [HttpPut("{id:guid}/cancellation-cutoff")]
    [Authorize]
    public async Task<ActionResult<BusinessResponse>> SetCancellationCutoff(
        Guid id, SetCancellationCutoffRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await businesses.SetCancellationCutoffAsync(id, CurrentUserId, request.Hours, ct));
        }
        catch (InvalidCancellationCutoffException ex)
        {
            return BadRequest(new { error = "invalid_cancellation_cutoff", message = ex.Message });
        }
        catch (BusinessNotFoundException ex)
        {
            return NotFound(new { error = "business_not_found", message = ex.Message });
        }
        catch (NotBusinessOwnerException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message });
        }
    }

    /// <summary>Configura los avisos del negocio (canales email/WhatsApp + recordatorio). Solo el owner.</summary>
    [HttpPut("{id:guid}/notification-settings")]
    [Authorize]
    public async Task<ActionResult<BusinessResponse>> SetNotificationSettings(
        Guid id, SetNotificationSettingsRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await businesses.SetNotificationSettingsAsync(id, CurrentUserId, request, ct));
        }
        catch (InvalidNotificationSettingsException ex)
        {
            return BadRequest(new { error = "invalid_notification_settings", message = ex.Message });
        }
        catch (BusinessNotFoundException ex)
        {
            return NotFound(new { error = "business_not_found", message = ex.Message });
        }
        catch (NotBusinessOwnerException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message });
        }
    }

    /// <summary>Cambia el modo de reservas ('online'|'calendar_only'). Solo el owner.</summary>
    [HttpPut("{id:guid}/booking-mode")]
    [Authorize]
    public async Task<ActionResult<BusinessResponse>> SetBookingMode(
        Guid id, SetBookingModeRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await businesses.SetBookingModeAsync(id, CurrentUserId, request.Mode, ct));
        }
        catch (InvalidBookingModeException ex)
        {
            return BadRequest(new { error = "invalid_booking_mode", message = ex.Message });
        }
        catch (BusinessNotFoundException ex)
        {
            return NotFound(new { error = "business_not_found", message = ex.Message });
        }
        catch (NotBusinessOwnerException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message });
        }
    }

    /// <summary>
    /// Cambia el plan del negocio. Solo el owner, y solo hacia 'free' (cancela la
    /// suscripción activa); el upgrade a Premium pasa por el checkout de pago (409).
    /// </summary>
    [HttpPut("{id:guid}/plan")]
    [Authorize]
    public async Task<ActionResult<BusinessResponse>> ChangePlan(
        Guid id, SetPlanRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(request.Code == "free"
                ? await subscriptions.DowngradeAsync(id, CurrentUserId, ct)
                : await businesses.ChangePlanAsync(id, CurrentUserId, request.Code, ct));
        }
        catch (InvalidPlanException ex)
        {
            return BadRequest(new { error = "invalid_plan", message = ex.Message });
        }
        catch (PaymentRequiredException ex)
        {
            return Conflict(new { error = "payment_required", message = ex.Message });
        }
        catch (BusinessNotFoundException ex)
        {
            return NotFound(new { error = "business_not_found", message = ex.Message });
        }
        catch (NotBusinessOwnerException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message });
        }
    }

    /// <summary>Actualiza el perfil público del negocio (categoría/foto/ubicación). Solo el owner.</summary>
    [HttpPut("{id:guid}/profile")]
    [Authorize]
    public async Task<ActionResult<BusinessResponse>> UpdateProfile(
        Guid id, UpdateBusinessProfileRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await businesses.UpdateProfileAsync(id, CurrentUserId, request, ct));
        }
        catch (InvalidCategoryException ex)
        {
            return BadRequest(new { error = "invalid_category", message = ex.Message });
        }
        catch (InvalidBusinessProfileException ex)
        {
            return BadRequest(new { error = "invalid_profile", message = ex.Message });
        }
        catch (BusinessNotFoundException ex)
        {
            return NotFound(new { error = "business_not_found", message = ex.Message });
        }
        catch (NotBusinessOwnerException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message });
        }
    }

    /// <summary>
    /// Listado/búsqueda pública de negocios activos (para que un cliente elija dónde
    /// reservar). Filtro opcional por nombre con <c>?q=</c> y por categoría con <c>?category=</c>.
    /// Paginado en BD con <c>?page=</c> (1-based, default 1) y <c>?pageSize=</c> (default 20,
    /// máx. 50); responde <c>{ items, total, page, pageSize }</c>.
    /// </summary>
    [HttpGet("/public/businesses")]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResponse<BusinessResponse>>> SearchPublic(
        [FromQuery] string? q, [FromQuery] string? category,
        [FromQuery] int page = 1, [FromQuery] int pageSize = BusinessService.DefaultPageSize,
        CancellationToken ct = default)
        => Ok(await businesses.SearchPublicAsync(q, category, page, pageSize, ct));
}
