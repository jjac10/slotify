using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Slotify.Domain.DTOs;
using Slotify.Domain.Services;

namespace Slotify.API.Controllers;

[ApiController]
[Route("businesses/{businessId:guid}/staff")]
public class StaffController(StaffService staff) : ControllerBase
{
    /// <summary>Lista los trabajadores activos de un negocio (público, para elegir con quién reservar).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<StaffResponse>>> List(Guid businessId, CancellationToken ct)
        => Ok(await staff.ListAsync(businessId, ct));
}
