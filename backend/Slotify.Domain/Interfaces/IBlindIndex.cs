namespace Slotify.Domain.Interfaces;

/// <summary>
/// Índice ciego (HMAC determinista) para buscar/garantizar unicidad sobre datos
/// cifrados sin exponerlos. El valor debe venir ya normalizado (ADR #5).
/// </summary>
public interface IBlindIndex
{
    /// <summary>Devuelve el hash hex (HMAC-SHA256) del valor normalizado.</summary>
    string Compute(string normalizedValue);
}
