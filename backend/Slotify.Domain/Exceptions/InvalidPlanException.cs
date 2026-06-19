namespace Slotify.Domain.Exceptions;

/// <summary>El código de plan debe ser 'free' o 'premium'. HTTP 400.</summary>
public class InvalidPlanException(string code)
    : Exception($"Plan inválido: '{code}' (debe ser 'free' o 'premium').");
