namespace Slotify.Domain;

/// <summary>
/// Categorías de negocio admitidas (lista fija). Se guarda el código; la etiqueta
/// para mostrar la resuelve el frontend. Ampliar aquí cuando haga falta.
/// </summary>
public static class BusinessCategories
{
    public static readonly IReadOnlySet<string> Codes = new HashSet<string>
    {
        "peluqueria",
        "barberia",
        "estetica",
        "unas",
        "spa",
        "depilacion",
        "maquillaje",
        "tatuajes",
        "fisioterapia",
        "otros",
    };

    public static bool IsValid(string code) => Codes.Contains(code);
}
