using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Slotify.Domain.DTOs;
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
}
