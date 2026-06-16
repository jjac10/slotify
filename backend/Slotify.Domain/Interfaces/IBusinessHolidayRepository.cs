using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>Acceso a datos de los días festivos de un negocio.</summary>
public interface IBusinessHolidayRepository
{
    Task AddAsync(BusinessHoliday holiday, CancellationToken ct = default);

    Task<IReadOnlyList<BusinessHoliday>> ListByBusinessAsync(Guid businessId, CancellationToken ct = default);

    Task<BusinessHoliday?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
