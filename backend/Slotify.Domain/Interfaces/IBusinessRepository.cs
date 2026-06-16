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
}
