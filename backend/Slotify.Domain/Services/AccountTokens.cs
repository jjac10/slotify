using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Slotify.Domain.Services;

/// <summary>
/// Tokens de un solo uso para emails de cuenta (recuperación de contraseña,
/// verificación de email): 256 bits aleatorios criptográficos en base64url; en BD
/// solo se persiste su hash SHA-256 (hex).
/// </summary>
internal static class AccountTokens
{
    private const int TokenBytes = 32; // 256 bits aleatorios criptográficos

    /// <summary>Genera un token nuevo (base64url, ≥43 caracteres).</summary>
    public static string NewToken()
        => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));

    /// <summary>Hash SHA-256 (hex) del token: lo único que se guarda en BD.</summary>
    public static string Sha256Hex(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
