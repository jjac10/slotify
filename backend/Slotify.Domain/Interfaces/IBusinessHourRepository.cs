using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>Acceso a datos del horario semanal de un negocio.</summary>
public interface IBusinessHourRepository
{
    Task<IReadOnlyList<BusinessHour>> ListByBusinessAsync(Guid businessId, CancellationToken ct = default);

    /// <summary>Reemplaza por completo el horario semanal del negocio (atómico).</summary>
    Task ReplaceForBusinessAsync(Guid businessId, IEnumerable<BusinessHour> hours, CancellationToken ct = default);
}
