namespace Slotify.Domain.Interfaces;

/// <summary>Persistencia de refresh tokens (se guardan hasheados; ADR #3).</summary>
public interface IRefreshTokenRepository
{
    /// <summary>Emite (persiste) un refresh token para el usuario.</summary>
    Task IssueAsync(Guid userId, string token, CancellationToken ct = default);

    /// <summary>
    /// Valida un refresh token activo y lo consume (rotación, un solo uso).
    /// Devuelve el userId si era válido; null si no existe o está caducado.
    /// </summary>
    Task<Guid?> ConsumeAsync(string token, CancellationToken ct = default);
}
