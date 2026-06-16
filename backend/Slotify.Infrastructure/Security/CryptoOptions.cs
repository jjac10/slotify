namespace Slotify.Infrastructure.Security;

/// <summary>
/// Claves de cifrado (sección "Crypto" en config). DOS claves distintas (ADR #5):
/// una para AES y otra para el blind index HMAC. Ambas en base64.
/// </summary>
public class CryptoOptions
{
    /// <summary>Clave AES-256 en base64 (32 bytes).</summary>
    public string EncryptionKey { get; set; } = null!;

    /// <summary>Clave HMAC del blind index en base64.</summary>
    public string BlindIndexKey { get; set; } = null!;
}
