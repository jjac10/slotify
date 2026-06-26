using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Slotify.Domain.DTOs;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Services;

namespace Slotify.API.Controllers;

[ApiController]
[Route("businesses/{businessId:guid}/services")]
public class ServicesController(ServiceService services) : ApiControllerBase
{
    /// <summary>Lista los servicios de un negocio (público).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ServiceResponse>>> List(Guid businessId, CancellationToken ct)
        => Ok(await services.ListAsync(businessId, ct));

    /// <summary>Crea un servicio (solo el owner, dentro del límite del plan).</summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ServiceResponse>> Create(Guid businessId, CreateServiceRequest request, CancellationToken ct)
    {
        try
        {
            var created = await services.CreateAsync(businessId, CurrentUserId, request, ct);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (BusinessNotFoundException ex)
        {
            return NotFound(new { error = "business_not_found", message = ex.Message });
        }
        catch (NotBusinessOwnerException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message });
        }
        catch (FreemiumLimitReachedException ex)
        {
            return Conflict(new { error = "limit_reached", message = ex.Message });
        }
    }

    /// <summary>Edita un servicio (solo el owner).</summary>
    [HttpPut("{serviceId:guid}")]
    [Authorize]
    public async Task<ActionResult<ServiceResponse>> Update(Guid businessId, Guid serviceId, UpdateServiceRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await services.UpdateAsync(businessId, serviceId, CurrentUserId, request, ct));
        }
        catch (BusinessNotFoundException ex)
        {
            return NotFound(new { error = "business_not_found", message = ex.Message });
        }
        catch (ServiceNotFoundException ex)
        {
            return NotFound(new { error = "service_not_found", message = ex.Message });
        }
        catch (NotBusinessOwnerException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message });
        }
    }

    /// <summary>Elimina un servicio (archivado lógico; solo el owner).</summary>
    [HttpDelete("{serviceId:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid businessId, Guid serviceId, CancellationToken ct)
    {
        try
        {
            await services.DeleteAsync(businessId, serviceId, CurrentUserId, ct);
            return NoContent();
        }
        catch (BusinessNotFoundException ex)
        {
            return NotFound(new { error = "business_not_found", message = ex.Message });
        }
        catch (ServiceNotFoundException ex)
        {
            return NotFound(new { error = "service_not_found", message = ex.Message });
        }
        catch (NotBusinessOwnerException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message });
        }
    }
}
