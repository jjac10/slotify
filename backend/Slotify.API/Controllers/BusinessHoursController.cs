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
        await schedule.SetHoursAsync(businessId, CurrentUserId, request.Days, ct);
        return Ok(await schedule.GetHoursAsync(businessId, ct));
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
        var created = await schedule.AddHolidayAsync(businessId, CurrentUserId, request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Elimina un festivo (solo owner).</summary>
    [HttpDelete("holidays/{holidayId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteHoliday(Guid businessId, Guid holidayId, CancellationToken ct)
    {
        await schedule.DeleteHolidayAsync(holidayId, CurrentUserId, ct);
        return NoContent();
    }
}
