using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Slotify.Domain.DTOs;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Services;

namespace Slotify.API.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    /// <summary>Registra un cliente (sin negocio).</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResult>> Register(RegisterCustomerRequest request, CancellationToken ct)
    {
        try
        {
            var result = await auth.RegisterCustomerAsync(request, ct);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (WeakPasswordException ex)
        {
            return BadRequest(new { error = "weak_password", message = ex.Message, details = ex.Errors });
        }
        catch (EmailAlreadyExistsException ex)
        {
            return Conflict(new { error = "email_exists", message = ex.Message });
        }
    }

    /// <summary>Registra un propietario y crea su negocio (plan Free) + owner-staff.</summary>
    [HttpPost("register-owner")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResult>> RegisterOwner(RegisterOwnerRequest request, CancellationToken ct)
    {
        try
        {
            var result = await auth.RegisterOwnerAsync(request, ct);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (WeakPasswordException ex)
        {
            return BadRequest(new { error = "weak_password", message = ex.Message, details = ex.Errors });
        }
        catch (EmailAlreadyExistsException ex)
        {
            return Conflict(new { error = "email_exists", message = ex.Message });
        }
    }

    /// <summary>Autentica con email + contraseña.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResult>> Login(LoginRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await auth.LoginAsync(request, ct));
        }
        catch (InvalidCredentialsException ex)
        {
            return Unauthorized(new { error = "invalid_credentials", message = ex.Message });
        }
    }

    /// <summary>Renueva el access token a partir de un refresh token válido.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResult>> Refresh(RefreshRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await auth.RefreshAsync(request.RefreshToken, ct));
        }
        catch (InvalidRefreshTokenException ex)
        {
            return Unauthorized(new { error = "invalid_refresh_token", message = ex.Message });
        }
    }

    /// <summary>Datos del usuario autenticado (requiere Bearer token).</summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<MeResponse> Me()
    {
        var id = User.FindFirstValue("sub");
        var email = User.FindFirstValue("email");
        if (id is null)
            return Unauthorized();

        return Ok(new MeResponse(Guid.Parse(id), email ?? string.Empty));
    }
}

public record MeResponse(Guid UserId, string Email);
