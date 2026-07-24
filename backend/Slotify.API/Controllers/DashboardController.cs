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
        return Ok(await dashboard.GetAsync(businessId, CurrentUserId, DateTime.UtcNow, ct));
    }
}
