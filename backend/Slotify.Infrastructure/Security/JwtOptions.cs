namespace Slotify.Infrastructure.Security;

/// <summary>Configuración del JWT (sección "Jwt" en appsettings).</summary>
public class JwtOptions
{
    public string Key { get; set; } = null!;
    public string Issuer { get; set; } = null!;
    public string Audience { get; set; } = null!;
    public int AccessTokenMinutes { get; set; } = 1440; // 24h (ADR #3)
    public int RefreshTokenDays { get; set; } = 7;       // (ADR #3)
}
