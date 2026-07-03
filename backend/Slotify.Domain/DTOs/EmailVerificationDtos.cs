namespace Slotify.Domain.DTOs;

/// <summary>POST /auth/verify-email — token del email simulado (un solo uso, 24 h).</summary>
public record VerifyEmailRequest(string Token);
