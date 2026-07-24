using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Slotify.Domain.DTOs;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Services;

namespace Slotify.API.Controllers;

/// <summary>
/// Moderación de la plataforma: SOLO para el admin configurado (Admin:Email). No da
/// de alta negocios (el registro es abierto para poder probar la demo): elimina los
/// de spam/prueba con borrado en cascada.
/// </summary>
[ApiController]
[Route("admin")]
[Authorize]
public class AdminController(BusinessDeletionService deletion, AdminOptions admin) : ApiControllerBase
{
    /// <summary>¿El usuario del token es el admin de plataforma? (claim email vs config).</summary>
    private bool IsAdmin() => admin.IsAdmin(User.FindFirstValue("email"));

    private ObjectResult NotAdmin() => StatusCode(StatusCodes.Status403Forbidden,
        new { error = "forbidden", message = "Solo el administrador de la plataforma puede hacer esto." });

    /// <summary>Directorio de negocios (búsqueda por nombre + paginación) para moderar.</summary>
    [HttpGet("businesses")]
    public async Task<ActionResult<PagedResponse<AdminBusinessResponse>>> ListBusinesses(
        [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int pageSize = BusinessDeletionService.DefaultPageSize,
        CancellationToken ct = default)
    {
        if (!IsAdmin()) return NotAdmin();

        return Ok(await deletion.ListForAdminAsync(q, page, pageSize, ct));
    }

    /// <summary>Elimina un negocio (spam/prueba) con todos sus datos, en cascada.</summary>
    [HttpPost("businesses/{businessId:guid}/delete")]
    public async Task<IActionResult> DeleteBusiness(Guid businessId, CancellationToken ct)
    {
        if (!IsAdmin()) return NotAdmin();

        await deletion.DeleteAsAdminAsync(businessId, ct);
        return NoContent();
    }
}
