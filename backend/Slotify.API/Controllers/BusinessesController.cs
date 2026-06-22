using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Slotify.Domain.DTOs;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Services;

namespace Slotify.API.Controllers;

[ApiController]
[Route("businesses")]
public class BusinessesController(BusinessService businesses) : ApiControllerBase
{
    /// <summary>Lista los negocios del owner autenticado.</summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<BusinessResponse>>> ListMine(CancellationToken ct)
        => Ok(await businesses.ListByOwnerAsync(CurrentUserId, ct));

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

    /// <summary>Cambia el plan del negocio ('free'|'premium'). Solo el owner. Upgrade simulado (TFM); en producción lo dispara la pasarela de pago.</summary>
    [HttpPut("{id:guid}/plan")]
    [Authorize]
    public async Task<ActionResult<BusinessResponse>> ChangePlan(
        Guid id, SetPlanRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await businesses.ChangePlanAsync(id, CurrentUserId, request.Code, ct));
        }
        catch (InvalidPlanException ex)
        {
            return BadRequest(new { error = "invalid_plan", message = ex.Message });
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
    /// </summary>
    [HttpGet("/public/businesses")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<BusinessResponse>>> SearchPublic(
        [FromQuery] string? q, [FromQuery] string? category, CancellationToken ct)
        => Ok(await businesses.SearchPublicAsync(q, category, ct));
}
