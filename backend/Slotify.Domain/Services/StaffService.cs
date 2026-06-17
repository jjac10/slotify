using Slotify.Domain.DTOs;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Listado de trabajadores de un negocio. Es público: el cliente lo usa para
/// elegir con quién reservar (el <c>staffId</c> que exige crear una reserva).
/// </summary>
public class StaffService(IStaffRepository staff)
{
    public async Task<IReadOnlyList<StaffResponse>> ListAsync(Guid businessId, CancellationToken ct = default)
    {
        var list = await staff.ListByBusinessAsync(businessId, ct);
        return list.Select(StaffResponse.From).ToList();
    }
}
