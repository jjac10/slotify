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

    /// <summary>
    /// Listado/búsqueda pública de negocios activos (para que un cliente elija dónde
    /// reservar). Filtro opcional por nombre con <c>?q=</c>.
    /// </summary>
    [HttpGet("/public/businesses")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<BusinessResponse>>> SearchPublic([FromQuery] string? q, CancellationToken ct)
        => Ok(await businesses.SearchPublicAsync(q, ct));
}
