namespace Slotify.Domain.DTOs;

/// <summary>POST /auth/forgot-password — siempre responde 200 genérico (anti-enumeración).</summary>
public record ForgotPasswordRequest(string Email);

/// <summary>POST /auth/reset-password — token del email simulado + nueva contraseña.</summary>
public record ResetPasswordRequest(string Token, string NewPassword);
