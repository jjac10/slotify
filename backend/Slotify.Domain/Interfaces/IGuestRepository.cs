using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>Acceso a datos de invitados (búsqueda por blind index, alta).</summary>
public interface IGuestRepository
{
    Task AddAsync(Guest guest, CancellationToken ct = default);

    /// <summary>
    /// Busca un guest del negocio cuyo phone_hash o email_hash coincida (dedupe).
    /// Devuelve null si no existe.
    /// </summary>
    Task<Guest?> FindByHashAsync(Guid businessId, string? phoneHash, string? emailHash, CancellationToken ct = default);
}
