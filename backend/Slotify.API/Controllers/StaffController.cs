using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Slotify.Domain.DTOs;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Services;

namespace Slotify.API.Controllers;

[ApiController]
[Route("businesses/{businessId:guid}/staff")]
public class StaffController(StaffService staff, StaffServiceAssignmentService assignments) : ApiControllerBase
{
    /// <summary>
    /// Lista los trabajadores activos de un negocio (público, para elegir con quién reservar).
    /// Con <c>?serviceId=</c> filtra a los que pueden realizar ese servicio.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<StaffResponse>>> List(Guid businessId, [FromQuery] Guid? serviceId, CancellationToken ct)
        => Ok(await staff.ListAsync(businessId, serviceId, ct));

    /// <summary>Da de alta un trabajador (solo el owner, dentro del límite del plan).</summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<StaffResponse>> Create(Guid businessId, CreateStaffRequest request, CancellationToken ct)
    {
        try
        {
            var created = await staff.CreateAsync(businessId, CurrentUserId, request, ct);
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

    /// <summary>Edita un trabajador (nombre/contacto; solo el owner).</summary>
    [HttpPatch("{staffId:guid}")]
    [Authorize]
    public async Task<ActionResult<StaffResponse>> Update(Guid businessId, Guid staffId, UpdateStaffRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await staff.UpdateAsync(businessId, staffId, CurrentUserId, request, ct));
        }
        catch (BusinessNotFoundException ex)
        {
            return NotFound(new { error = "business_not_found", message = ex.Message });
        }
        catch (StaffNotFoundException ex)
        {
            return NotFound(new { error = "staff_not_found", message = ex.Message });
        }
        catch (NotBusinessOwnerException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message });
        }
    }

    /// <summary>Da de baja (lógica) a un trabajador; el owner no se puede dar de baja (solo el owner).</summary>
    [HttpDelete("{staffId:guid}")]
    [Authorize]
    public async Task<IActionResult> Deactivate(Guid businessId, Guid staffId, CancellationToken ct)
    {
        try
        {
            await staff.DeactivateAsync(businessId, staffId, CurrentUserId, ct);
            return NoContent();
        }
        catch (BusinessNotFoundException ex)
        {
            return NotFound(new { error = "business_not_found", message = ex.Message });
        }
        catch (StaffNotFoundException ex)
        {
            return NotFound(new { error = "staff_not_found", message = ex.Message });
        }
        catch (NotBusinessOwnerException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message });
        }
        catch (CannotModifyOwnerStaffException ex)
        {
            return Conflict(new { error = "cannot_remove_owner", message = ex.Message });
        }
    }

    /// <summary>Servicios que puede realizar un trabajador (ids; solo el owner).</summary>
    [HttpGet("{staffId:guid}/services")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<Guid>>> GetServices(Guid businessId, Guid staffId, CancellationToken ct)
    {
        try
        {
            return Ok(await assignments.ListAsync(businessId, staffId, CurrentUserId, ct));
        }
        catch (BusinessNotFoundException ex)
        {
            return NotFound(new { error = "business_not_found", message = ex.Message });
        }
        catch (StaffNotFoundException ex)
        {
            return NotFound(new { error = "staff_not_found", message = ex.Message });
        }
        catch (NotBusinessOwnerException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message });
        }
    }

    /// <summary>Fija los servicios que puede realizar un trabajador (reemplaza; solo el owner).</summary>
    [HttpPut("{staffId:guid}/services")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<Guid>>> SetServices(
        Guid businessId, Guid staffId, SetStaffServicesRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await assignments.SetAsync(businessId, staffId, CurrentUserId, request.ServiceIds, ct));
        }
        catch (BusinessNotFoundException ex)
        {
            return NotFound(new { error = "business_not_found", message = ex.Message });
        }
        catch (StaffNotFoundException ex)
        {
            return NotFound(new { error = "staff_not_found", message = ex.Message });
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
