namespace Slotify.API;

/// <summary>
/// Configuración del rate limiting (sección "RateLimiting" en appsettings).
/// Defaults pensados para producción: 10 peticiones/60s por IP en los endpoints de auth.
/// </summary>
public class RateLimitingOptions
{
    public int AuthPermitLimit { get; set; } = 10;
    public int AuthWindowSeconds { get; set; } = 60;
}
