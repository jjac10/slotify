namespace Slotify.Domain.DTOs;

/// <summary>
/// DELETE /auth/me — confirmación del borrado de cuenta con la contraseña actual
/// (derecho de supresión RGPD). La contraseña viaja en el body, nunca en la URL.
/// </summary>
public record DeleteAccountRequest(string Password);
