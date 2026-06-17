using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Slotify.Domain.DTOs;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Services;

namespace Slotify.API.Controllers;

[ApiController]
[Route("businesses/{businessId:guid}/dashboard")]
public class DashboardController(DashboardService dashboard) : ApiControllerBase
{
    /// <summary>Resumen del negocio para su propietario (solo el owner).</summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<DashboardResponse>> Get(Guid businessId, CancellationToken ct)
    {
        try
        {
            return Ok(await dashboard.GetAsync(businessId, CurrentUserId, DateTime.UtcNow, ct));
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
}
