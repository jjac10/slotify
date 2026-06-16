using System.Security.Cryptography;
using System.Text;
using Slotify.Domain.Interfaces;

namespace Slotify.Infrastructure.Security;

/// <summary>Blind index con HMAC-SHA256 determinista (clave distinta a la de AES, ADR #5).</summary>
public class HmacBlindIndex(CryptoOptions options) : IBlindIndex
{
    private readonly byte[] _key = Convert.FromBase64String(options.BlindIndexKey);

    public string Compute(string normalizedValue)
    {
        using var hmac = new HMACSHA256(_key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(normalizedValue));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
