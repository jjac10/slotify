using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Gestión de trabajadores de un negocio. El listado es público (el cliente elige
/// con quién reservar). El alta/edición/baja es del owner y respeta el límite
/// Freemium del plan (ADR #9); el owner-staff no se puede dar de baja.
/// </summary>
public class StaffService(
    IStaffRepository staff,
    IBusinessRepository businesses,
    IFreemiumLimitService limits)
{
    public async Task<IReadOnlyList<StaffResponse>> ListAsync(Guid businessId, CancellationToken ct = default)
    {
        var list = await staff.ListByBusinessAsync(businessId, ct);
        return list.Select(StaffResponse.From).ToList();
    }

    public async Task<StaffResponse> CreateAsync(
        Guid businessId, Guid currentUserId, CreateStaffRequest request, CancellationToken ct = default)
    {
        await EnsureOwnerAsync(businessId, currentUserId, ct);

        if (!await limits.CanAddStaffAsync(businessId, ct))
            throw new FreemiumLimitReachedException("trabajadores");

        var member = new Staff
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Role = "employee",
            Name = request.Name,
            Email = request.Email,
            Phone = request.Phone,
            Status = "active",
            CreatedAt = DateTime.UtcNow,
        };
        await staff.AddAsync(member, ct);

        return StaffResponse.From(member);
    }

    public async Task<StaffResponse> UpdateAsync(
        Guid businessId, Guid staffId, Guid currentUserId, UpdateStaffRequest request, CancellationToken ct = default)
    {
        await EnsureOwnerAsync(businessId, currentUserId, ct);

        var member = await staff.GetByIdAsync(staffId, ct);
        if (member is null || member.BusinessId != businessId)
            throw new StaffNotFoundException(staffId);

        member.Name = request.Name;
        member.Email = request.Email;
        member.Phone = request.Phone;
        await staff.UpdateAsync(member, ct);

        return StaffResponse.From(member);
    }

    public async Task DeactivateAsync(
        Guid businessId, Guid staffId, Guid currentUserId, CancellationToken ct = default)
    {
        await EnsureOwnerAsync(businessId, currentUserId, ct);

        var member = await staff.GetByIdAsync(staffId, ct);
        if (member is null || member.BusinessId != businessId)
            throw new StaffNotFoundException(staffId);
        if (member.Role == "owner")
            throw new CannotModifyOwnerStaffException();

        member.Status = "inactive";
        await staff.UpdateAsync(member, ct);
    }

    private async Task EnsureOwnerAsync(Guid businessId, Guid currentUserId, CancellationToken ct)
    {
        var business = await businesses.GetByIdAsync(businessId, ct)
            ?? throw new BusinessNotFoundException(businessId);

        if (business.OwnerId != currentUserId)
            throw new NotBusinessOwnerException();
    }
}
