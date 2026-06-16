using Slotify.Domain.Exceptions;

namespace Slotify.Domain.Services;

/// <summary>
/// Política de contraseña segura (RF-AUTH-001): mínimo 8 caracteres con
/// mayúscula, minúscula, dígito y símbolo. Lanza <see cref="WeakPasswordException"/>.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    public static void Validate(string password)
    {
        var errors = new List<string>();
        password ??= string.Empty;

        if (password.Length < MinLength)
            errors.Add($"Debe tener al menos {MinLength} caracteres.");
        if (!password.Any(char.IsUpper))
            errors.Add("Debe incluir una mayúscula.");
        if (!password.Any(char.IsLower))
            errors.Add("Debe incluir una minúscula.");
        if (!password.Any(char.IsDigit))
            errors.Add("Debe incluir un dígito.");
        if (password.All(char.IsLetterOrDigit))
            errors.Add("Debe incluir un símbolo.");

        if (errors.Count > 0)
            throw new WeakPasswordException(errors);
    }
}
