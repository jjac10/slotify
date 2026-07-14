using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

public interface IWaitlistRepository
{
    Task AddAsync(WaitlistEntry entry, CancellationToken ct = default);

    /// <summary>Entrada por id (con negocio y servicio cargados), o null.</summary>
    Task<WaitlistEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Entradas del usuario (con negocio y servicio), próximas primero.</summary>
    Task<IReadOnlyList<WaitlistEntry>> ListByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>¿El usuario ya espera para ese (servicio, día)? (en cualquier estado).</summary>
    Task<bool> ExistsForUserAsync(Guid serviceId, DateOnly date, Guid userId, CancellationToken ct = default);

    /// <summary>Nº de entradas en cola (waiting) de un (servicio, día) — para la posición.</summary>
    Task<int> CountWaitingAsync(Guid serviceId, DateOnly date, CancellationToken ct = default);

    /// <summary>El primero en cola (waiting, menor posición) de un (servicio, día), o null.</summary>
    Task<WaitlistEntry?> FirstWaitingAsync(Guid serviceId, DateOnly date, CancellationToken ct = default);

    Task UpdateAsync(WaitlistEntry entry, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// ¿Queda algún hueco libre ese día para ese servicio (con cualquier trabajador)?
/// Adaptador sobre <c>AvailabilityService</c> — interfaz aparte para poder testear
/// <c>WaitlistService</c> sin recalcular disponibilidad real.
/// </summary>
public interface IDayAvailabilityChecker
{
    Task<bool> HasFreeSlotAsync(Guid businessId, Guid serviceId, DateOnly date, CancellationToken ct = default);
}
