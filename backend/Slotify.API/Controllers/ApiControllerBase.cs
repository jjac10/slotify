using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Slotify.API.Controllers;

/// <summary>Base con utilidades comunes para los controllers autenticados.</summary>
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Id del usuario autenticado (claim 'sub' del JWT).</summary>
    protected Guid CurrentUserId => Guid.Parse(User.FindFirstValue("sub")!);

    /// <summary>Id del usuario si la petición trae JWT; null si es anónima (p. ej. invitado).</summary>
    protected Guid? TryGetUserId() => User.FindFirstValue("sub") is { } sub ? Guid.Parse(sub) : null;
}
