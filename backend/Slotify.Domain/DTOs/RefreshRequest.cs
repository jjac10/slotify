namespace Slotify.Domain.DTOs;

/// <summary>Petición de renovación de tokens (API.md POST /auth/refresh).</summary>
public record RefreshRequest(string RefreshToken);
