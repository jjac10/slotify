using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Security;

namespace Slotify.Tests.Unit;

/// <summary>Hashing de contraseñas con bcrypt (RF-AUTH-001).</summary>
public class PasswordHasherTests
{
    private readonly IPasswordHasher _hasher = new BcryptPasswordHasher();

    [Fact]
    public void Hash_DoesNotReturnPlaintext_AndVerifiesTrue()
    {
        const string password = "SecurePass123!";

        var hash = _hasher.Hash(password);

        Assert.NotEqual(password, hash);
        Assert.True(_hasher.Verify(password, hash));
    }

    [Fact]
    public void Verify_WithWrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash("SecurePass123!");

        Assert.False(_hasher.Verify("WrongPassword", hash));
    }
}
