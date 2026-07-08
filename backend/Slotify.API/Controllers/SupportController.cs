using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Slotify.Domain.DTOs;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Services;

namespace Slotify.API.Controllers;

[ApiController]
[Route("support")]
public class SupportController(SupportService support) : ControllerBase
{
    /// <summary>
    /// Formulario público de contacto/soporte. "Envía" (simulado) el mensaje al equipo
    /// de Slotify; no persiste nada. Con la misma política de rate limiting por IP que
    /// auth (anti-spam): al superarla responde 429 + Retry-After.
    /// </summary>
    [HttpPost("contact")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Contact(ContactSupportRequest request, CancellationToken ct)
    {
        try
        {
            await support.SendContactMessageAsync(request.Name, request.Email, request.Message, ct);
            return NoContent();
        }
        catch (InvalidContactMessageException ex)
        {
            return BadRequest(new { error = "invalid_contact_message", message = ex.Message, details = ex.Errors });
        }
    }
}
