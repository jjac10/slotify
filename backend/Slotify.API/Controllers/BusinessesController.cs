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

    /// <summary>
    /// Listado/búsqueda pública de negocios activos (para que un cliente elija dónde
    /// reservar). Filtro opcional por nombre con <c>?q=</c>.
    /// </summary>
    [HttpGet("/public/businesses")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<BusinessResponse>>> SearchPublic([FromQuery] string? q, CancellationToken ct)
        => Ok(await businesses.SearchPublicAsync(q, ct));
}
