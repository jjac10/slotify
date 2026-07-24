using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Slotify.Domain.DTOs;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Services;

namespace Slotify.API.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(
    AuthService auth,
    PasswordResetService passwordReset,
    EmailVerificationService emailVerification,
    AccountDeletionService accountDeletion,
    AdminOptions admin) : ControllerBase
{
    private const string ForgotPasswordGenericMessage =
        "Si el email existe, recibirás instrucciones para restablecer tu contraseña.";

    /// <summary>Registra un cliente (sin negocio).</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResult>> Register(RegisterCustomerRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterCustomerAsync(request, ct);
        // Verificación de email NO bloqueante: si el "envío" falla, el registro no falla.
        await emailVerification.TrySendAsync(result.UserId, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Registra un propietario y crea su negocio (plan Free) + owner-staff.</summary>
    [HttpPost("register-owner")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResult>> RegisterOwner(RegisterOwnerRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterOwnerAsync(request, ct);
        // Verificación de email NO bloqueante: si el "envío" falla, el registro no falla.
        await emailVerification.TrySendAsync(result.UserId, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Datos de una invitación de empleado pendiente (para la pantalla de aceptar). Público.</summary>
    [HttpGet("staff-invite/{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<StaffInviteInfoResponse>> GetStaffInvite(string token, CancellationToken ct)
    {
        return Ok(await auth.GetStaffInviteAsync(token, ct));
    }

    /// <summary>El empleado fija su contraseña y crea su cuenta a partir del token de invitación. Público.</summary>
    [HttpPost("staff-invite/{token}/accept")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResult>> AcceptStaffInvite(string token, AcceptStaffInviteRequest request, CancellationToken ct)
    {
        return StatusCode(StatusCodes.Status201Created, await auth.AcceptStaffInviteAsync(token, request.Password, ct));
    }

    /// <summary>Autentica con email + contraseña.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResult>> Login(LoginRequest request, CancellationToken ct)
    {
        return Ok(await auth.LoginAsync(request, ct));
    }

    /// <summary>
    /// Solicita la recuperación de contraseña. Responde SIEMPRE 200 con un mensaje
    /// genérico (exista o no el email) para no permitir enumerar usuarios; el token
    /// viaja solo en el email (simulado), nunca en esta respuesta.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await passwordReset.RequestResetAsync(request.Email, ct);
        return Ok(new { message = ForgotPasswordGenericMessage });
    }

    /// <summary>Restablece la contraseña con un token de recuperación válido (1 h, un solo uso).</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await passwordReset.ResetPasswordAsync(request.Token, request.NewPassword, ct);
        return Ok(new { message = "Contraseña actualizada. Ya puedes iniciar sesión." });
    }

    /// <summary>Verifica el email con el token del enlace (24 h, un solo uso). Público.</summary>
    [HttpPost("verify-email")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken ct)
    {
        await emailVerification.VerifyAsync(request.Token, ct);
        return Ok(new { message = "Email verificado. ¡Gracias!" });
    }

    /// <summary>Reenvía el email de verificación al usuario autenticado (regenera el token).</summary>
    [HttpPost("resend-verification")]
    [Authorize]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendVerification(CancellationToken ct)
    {
        var id = User.FindFirstValue("sub");
        if (id is null)
            return Unauthorized();

        await emailVerification.ResendAsync(Guid.Parse(id), ct);
        return Ok(new { message = "Te hemos reenviado el enlace de verificación." });
    }

    /// <summary>Renueva el access token a partir de un refresh token válido.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResult>> Refresh(RefreshRequest request, CancellationToken ct)
    {
        return Ok(await auth.RefreshAsync(request.RefreshToken, ct));
    }

    /// <summary>
    /// Borra la cuenta del usuario autenticado (derecho de supresión RGPD), previa
    /// confirmación con su contraseña actual. La contraseña incorrecta devuelve 400
    /// (no 401: el JWT es válido y un 401 haría que el frontend cerrara la sesión).
    /// Un owner con reservas futuras de clientes recibe 409.
    /// </summary>
    [HttpDelete("me")]
    [Authorize]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> DeleteMe(DeleteAccountRequest request, CancellationToken ct)
    {
        var id = User.FindFirstValue("sub");
        if (id is null)
            return Unauthorized();

        try
        {
            await accountDeletion.DeleteAccountAsync(Guid.Parse(id), request.Password, ct);
            return NoContent();
        }
        catch (InvalidCredentialsException)
        {
            return BadRequest(new { error = "invalid_password", message = "La contraseña no es correcta." });
        }
    }

    /// <summary>Datos del usuario autenticado (requiere Bearer token).</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken ct)
    {
        var id = User.FindFirstValue("sub");
        var email = User.FindFirstValue("email");
        if (id is null)
            return Unauthorized();

        var userId = Guid.Parse(id);
        var emailVerified = await emailVerification.IsVerifiedAsync(userId, ct);
        return Ok(new MeResponse(userId, email ?? string.Empty, emailVerified, admin.IsAdmin(email)));
    }
}

public record MeResponse(Guid UserId, string Email, bool EmailVerified, bool IsAdmin = false);
