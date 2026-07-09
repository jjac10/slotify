using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Slotify.Domain.DTOs;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Services;

namespace Slotify.API.Controllers;

[ApiController]
[Route("businesses/{businessId:guid}/availability")]
public class AvailabilityController(AvailabilityService availability) : ControllerBase
{
    /// <summary>Slots libres de un servicio para un trabajador y una fecha (público).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<AvailableSlot>>> Get(
        Guid businessId,
        [FromQuery] Guid serviceId,
        [FromQuery] Guid staffId,
        [FromQuery] DateOnly date,
        CancellationToken ct)
    {
        try
        {
            return Ok(await availability.GetSlotsAsync(businessId, serviceId, staffId, date, DateTime.UtcNow, ct));
        }
        catch (BusinessNotFoundException ex) { return NotFound(new { error = "business_not_found", message = ex.Message }); }
        catch (ServiceNotFoundException ex) { return NotFound(new { error = "service_not_found", message = ex.Message }); }
        catch (StaffNotFoundException ex) { return NotFound(new { error = "staff_not_found", message = ex.Message }); }
    }

    /// <summary>
    /// Estado de cada día de un mes ('closed' | 'full' | 'available') para pintar el
    /// calendario de reserva en verde/rojo (público).
    /// </summary>
    [HttpGet("month")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<DayAvailability>>> GetMonth(
        Guid businessId,
        [FromQuery] Guid serviceId,
        [FromQuery] Guid staffId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken ct)
    {
        if (month is < 1 or > 12 || year is < 2000 or > 2100)
            return BadRequest(new { error = "invalid_month", message = "El mes debe estar entre 1 y 12 y el año ser razonable." });

        try
        {
            return Ok(await availability.GetMonthAsync(businessId, serviceId, staffId, year, month, DateTime.UtcNow, ct));
        }
        catch (BusinessNotFoundException ex) { return NotFound(new { error = "business_not_found", message = ex.Message }); }
        catch (ServiceNotFoundException ex) { return NotFound(new { error = "service_not_found", message = ex.Message }); }
        catch (StaffNotFoundException ex) { return NotFound(new { error = "staff_not_found", message = ex.Message }); }
    }
}
