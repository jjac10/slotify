using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Slotify.Domain.DTOs;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Services;

namespace Slotify.API.Controllers;

[ApiController]
[Route("businesses/{businessId:guid}")]
public class BusinessHoursController(BusinessScheduleService schedule) : ApiControllerBase
{
    /// <summary>Horario semanal del negocio (público).</summary>
    [HttpGet("hours")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<BusinessHourResponse>>> GetHours(Guid businessId, CancellationToken ct)
        => Ok(await schedule.GetHoursAsync(businessId, ct));

    /// <summary>Fija el horario semanal completo (solo owner).</summary>
    [HttpPut("hours")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<BusinessHourResponse>>> SetHours(
        Guid businessId, SetBusinessHoursRequest request, CancellationToken ct)
    {
        try
        {
            await schedule.SetHoursAsync(businessId, CurrentUserId, request.Days, ct);
            return Ok(await schedule.GetHoursAsync(businessId, ct));
        }
        catch (BusinessNotFoundException ex) { return NotFound(new { error = "business_not_found", message = ex.Message }); }
        catch (NotBusinessOwnerException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message }); }
        catch (InvalidBusinessHoursException ex) { return BadRequest(new { error = "invalid_hours", message = ex.Message }); }
    }

    /// <summary>Festivos del negocio (público).</summary>
    [HttpGet("holidays")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<BusinessHolidayResponse>>> GetHolidays(Guid businessId, CancellationToken ct)
        => Ok(await schedule.ListHolidaysAsync(businessId, ct));

    /// <summary>Añade un festivo (solo owner).</summary>
    [HttpPost("holidays")]
    [Authorize]
    public async Task<ActionResult<BusinessHolidayResponse>> AddHoliday(
        Guid businessId, CreateHolidayRequest request, CancellationToken ct)
    {
        try
        {
            var created = await schedule.AddHolidayAsync(businessId, CurrentUserId, request, ct);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (InvalidHolidayException ex) { return BadRequest(new { error = "invalid_holiday", message = ex.Message }); }
        catch (BusinessNotFoundException ex) { return NotFound(new { error = "business_not_found", message = ex.Message }); }
        catch (NotBusinessOwnerException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message }); }
    }

    /// <summary>Elimina un festivo (solo owner).</summary>
    [HttpDelete("holidays/{holidayId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteHoliday(Guid businessId, Guid holidayId, CancellationToken ct)
    {
        try
        {
            await schedule.DeleteHolidayAsync(holidayId, CurrentUserId, ct);
            return NoContent();
        }
        catch (HolidayNotFoundException ex) { return NotFound(new { error = "holiday_not_found", message = ex.Message }); }
        catch (NotBusinessOwnerException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message }); }
    }
}
