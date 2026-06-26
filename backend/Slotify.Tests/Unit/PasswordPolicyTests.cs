using Slotify.Domain.Exceptions;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>Política de contraseña segura en el registro (RF-AUTH-001).</summary>
public class PasswordPolicyTests
{
    [Fact]
    public void Validate_StrongPassword_DoesNotThrow()
    {
        // 8+ chars, mayúscula, minúscula, dígito y símbolo.
        PasswordPolicy.Validate("SecurePass123!");
    }

    [Theory]
    [InlineData("Ab1!")]          // demasiado corta
    [InlineData("securepass123!")] // sin mayúscula
    [InlineData("SECUREPASS123!")] // sin minúscula
    [InlineData("SecurePassword!")] // sin dígito
    [InlineData("SecurePass1234")]  // sin símbolo
    [InlineData("")]                // vacía
    public void Validate_WeakPassword_Throws(string password)
    {
        Assert.Throws<WeakPasswordException>(() => PasswordPolicy.Validate(password));
    }
}
