using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Lógica de negocio para businesses. Al crear un negocio crea también su
/// owner-as-staff (role='owner'), de modo que toda reserva pueda tener staff_id
/// no nulo. Schema/decisión: docs/DATA_MODEL.md (staff).
/// </summary>
public class BusinessService(IBusinessRepository repository)
{
    public async Task<Business> CreateAsync(CreateBusinessRequest request, CancellationToken ct = default)
    {
        var business = new Business
        {
            Id = Guid.NewGuid(),
            OwnerId = request.OwnerId,
            TierId = request.TierId,
            Name = request.Name,
        };

        var ownerStaff = new Staff
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            UserId = request.OwnerId,
            Role = "owner",
            Name = request.OwnerName,
        };

        await repository.AddWithOwnerStaffAsync(business, ownerStaff, ct);
        return business;
    }

    /// <summary>Lista los negocios de un owner.</summary>
    public async Task<IReadOnlyList<BusinessResponse>> ListByOwnerAsync(Guid ownerId, CancellationToken ct = default)
    {
        var list = await repository.ListByOwnerAsync(ownerId, ct);
        return list.Select(BusinessResponse.From).ToList();
    }

    /// <summary>Listado/búsqueda pública de negocios (para que el cliente elija dónde reservar).</summary>
    public async Task<IReadOnlyList<BusinessResponse>> SearchPublicAsync(string? query, CancellationToken ct = default)
    {
        var list = await repository.SearchPublicAsync(query, ct);
        return list.Select(BusinessResponse.From).ToList();
    }
}
