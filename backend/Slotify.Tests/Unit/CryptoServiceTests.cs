using System.Security.Cryptography;
using Slotify.Infrastructure.Security;

namespace Slotify.Tests.Unit;

/// <summary>
/// Cifrado de datos sensibles de invitado (ADR #5): AES-256-GCM recuperable y
/// blind index HMAC determinista para búsqueda/unicidad.
/// </summary>
public class CryptoServiceTests
{
    private static CryptoOptions NewOptions() => new()
    {
        EncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        BlindIndexKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
    };

    [Fact]
    public void Encrypt_ThenDecrypt_RoundTrips()
    {
        var crypto = new AesGcmCryptoService(NewOptions());

        var cipher = crypto.Encrypt("+34912345678");

        Assert.NotEqual("+34912345678", cipher);
        Assert.Equal("+34912345678", crypto.Decrypt(cipher));
    }

    [Fact]
    public void Encrypt_SamePlaintextTwice_ProducesDifferentCiphertext()
    {
        var crypto = new AesGcmCryptoService(NewOptions());

        // IV aleatorio → cada cifrado es distinto (por eso hace falta el blind index).
        Assert.NotEqual(crypto.Encrypt("hola"), crypto.Encrypt("hola"));
    }

    [Fact]
    public void BlindIndex_IsDeterministic_AndDistinguishesValues()
    {
        var index = new HmacBlindIndex(NewOptions());

        var hash = index.Compute("juan@example.com");

        Assert.Equal(hash, index.Compute("juan@example.com")); // determinista
        Assert.NotEqual(hash, index.Compute("otro@example.com"));
        Assert.Equal(64, hash.Length); // SHA-256 en hex
    }
}
