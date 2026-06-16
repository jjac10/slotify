namespace Slotify.Domain.Interfaces;

/// <summary>Validación de límites del plan (Freemium), data-driven (ADR #9).</summary>
public interface IFreemiumLimitService
{
    Task<bool> CanAddStaffAsync(Guid businessId, CancellationToken ct = default);
    Task<bool> CanAddServiceAsync(Guid businessId, CancellationToken ct = default);
}
