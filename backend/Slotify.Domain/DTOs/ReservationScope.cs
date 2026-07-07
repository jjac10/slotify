namespace Slotify.Domain.DTOs;

/// <summary>
/// Ámbito temporal de "mis reservas" (<c>?scope=</c>). Las canceladas no existen como
/// estado persistido (se hard-deletean, ADR #13), así que el filtro es solo temporal
/// sobre el inicio de la reserva respecto al "ahora" UTC.
/// </summary>
public enum ReservationScope
{
    /// <summary>Todas (comportamiento por defecto), orden ascendente por inicio.</summary>
    All,

    /// <summary>Inicio &gt;= ahora UTC, orden ascendente (la más próxima primero).</summary>
    Upcoming,

    /// <summary>Inicio &lt; ahora UTC, orden descendente (la más reciente primero).</summary>
    Past,
}
