using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>
/// Abstracción de persistencia de negocios (Repository Pattern, ADR #2).
/// Mantiene la BD intercambiable sin tocar la capa de servicio.
/// </summary>
public interface IBusinessRepository
{
    /// <summary>
    /// Persiste el negocio junto a su staff owner de forma atómica (una sola
    /// transacción): todo negocio nace con su owner-as-staff.
    /// </summary>
    Task AddWithOwnerStaffAsync(Business business, Staff ownerStaff, CancellationToken ct = default);

    Task<Business?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Persiste cambios de un negocio ya cargado (p. ej. ajustes del owner).</summary>
    Task UpdateAsync(Business business, CancellationToken ct = default);

    /// <summary>Lista los negocios de un owner.</summary>
    Task<IReadOnlyList<Business>> ListByOwnerAsync(Guid ownerId, CancellationToken ct = default);

    /// <summary>
    /// Listado público de negocios activos para que un cliente busque dónde reservar.
    /// Si <paramref name="query"/> trae texto, filtra por nombre (contiene, sin distinguir
    /// mayúsculas). Ordenado por nombre.
    /// </summary>
    Task<IReadOnlyList<Business>> SearchPublicAsync(string? query, CancellationToken ct = default);
}
