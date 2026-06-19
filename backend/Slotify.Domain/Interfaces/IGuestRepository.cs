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

    /// <summary>
    /// Ids de todos los guests (en cualquier negocio) cuyo phone_hash o email_hash
    /// coincida con <paramref name="hash"/>. Para que un invitado vea sus reservas por contacto.
    /// </summary>
    Task<IReadOnlyList<Guid>> FindIdsByContactHashAsync(string hash, CancellationToken ct = default);

    /// <summary>
    /// Vincula al usuario los guests (aún sin user) cuyo phone_hash o email_hash
    /// coincida — en todos los negocios. Devuelve cuántos se enlazaron (sync invitado→usuario).
    /// </summary>
    Task<int> LinkToUserByHashAsync(Guid userId, string? phoneHash, string? emailHash, CancellationToken ct = default);
}
