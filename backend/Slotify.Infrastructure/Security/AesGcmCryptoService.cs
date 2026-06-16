using System.Security.Cryptography;
using System.Text;
using Slotify.Domain.Interfaces;

namespace Slotify.Infrastructure.Security;

/// <summary>
/// AES-256-GCM. Formato de salida: base64( IV(12) + Tag(16) + Ciphertext ).
/// IV aleatorio por operación → el ciphertext cambia siempre (ADR #5).
/// </summary>
public class AesGcmCryptoService(CryptoOptions options) : ICryptoService
{
    private const int IvSize = 12;
    private const int TagSize = 16;
    private readonly byte[] _key = Convert.FromBase64String(options.EncryptionKey);

    public string Encrypt(string plaintext)
    {
        var iv = RandomNumberGenerator.GetBytes(IvSize);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(iv, plain, cipher, tag);

        var output = new byte[IvSize + TagSize + cipher.Length];
        Buffer.BlockCopy(iv, 0, output, 0, IvSize);
        Buffer.BlockCopy(tag, 0, output, IvSize, TagSize);
        Buffer.BlockCopy(cipher, 0, output, IvSize + TagSize, cipher.Length);
        return Convert.ToBase64String(output);
    }

    public string Decrypt(string ciphertext)
    {
        var data = Convert.FromBase64String(ciphertext);
        var iv = data.AsSpan(0, IvSize);
        var tag = data.AsSpan(IvSize, TagSize);
        var cipher = data.AsSpan(IvSize + TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(iv, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
