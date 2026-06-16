namespace Slotify.Domain.Interfaces;

/// <summary>Cifrado simétrico recuperable de datos sensibles (AES-256-GCM, ADR #5).</summary>
public interface ICryptoService
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}
