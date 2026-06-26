using Slotify.Domain.Interfaces;

namespace Slotify.Infrastructure.Security;

/// <summary>Implementación de <see cref="IPasswordHasher"/> con bcrypt (ADR #5 / RF-AUTH-001).</summary>
public class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
