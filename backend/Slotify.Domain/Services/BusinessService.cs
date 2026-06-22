using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

// BusinessCategories vive en Slotify.Domain

/// <summary>
/// Lógica de negocio para businesses. Al crear un negocio crea también su
/// owner-as-staff (role='owner'), de modo que toda reserva pueda tener staff_id
/// no nulo. Schema/decisión: docs/DATA_MODEL.md (staff).
/// </summary>
public class BusinessService(IBusinessRepository repository, ITierRepository tiers)
{
    private static readonly string[] ValidPlanCodes = ["free", "premium"];

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

    /// <summary>Listado/búsqueda pública de negocios (nombre + categoría opcional).</summary>
    public async Task<IReadOnlyList<BusinessResponse>> SearchPublicAsync(string? query, string? category = null, CancellationToken ct = default)
    {
        var list = await repository.SearchPublicAsync(query, category, ct);
        return list.Select(BusinessResponse.From).ToList();
    }

    /// <summary>Actualiza el perfil público del negocio (categoría/foto/ubicación). Solo el owner.</summary>
    public async Task<BusinessResponse> UpdateProfileAsync(
        Guid businessId, Guid userId, UpdateBusinessProfileRequest request, CancellationToken ct = default)
    {
        if (request.Category is { } cat && !BusinessCategories.IsValid(cat))
            throw new InvalidCategoryException(cat);

        var business = await repository.GetByIdAsync(businessId, ct)
            ?? throw new BusinessNotFoundException(businessId);
        if (business.OwnerId != userId)
            throw new NotBusinessOwnerException();

        business.Category = request.Category;
        business.PhotoUrl = request.PhotoUrl;
        business.Latitude = request.Latitude;
        business.Longitude = request.Longitude;
        await repository.UpdateAsync(business, ct);

        return BusinessResponse.From(business);
    }

    /// <summary>
    /// Cambia el modo de confirmación del negocio ('auto'|'manual'). Solo el owner.
    /// </summary>
    public async Task<BusinessResponse> SetConfirmationModeAsync(
        Guid businessId, Guid userId, string mode, CancellationToken ct = default)
    {
        if (mode is not ("auto" or "manual"))
            throw new InvalidConfirmationModeException(mode);

        var business = await repository.GetByIdAsync(businessId, ct)
            ?? throw new BusinessNotFoundException(businessId);
        if (business.OwnerId != userId)
            throw new NotBusinessOwnerException();

        business.ConfirmationMode = mode;
        await repository.UpdateAsync(business, ct);
        return BusinessResponse.From(business);
    }

    /// <summary>
    /// Fija la antelación mínima (en horas) para que el cliente cancele/reprograme.
    /// 0 = sin restricción. Solo el owner.
    /// </summary>
    public async Task<BusinessResponse> SetCancellationCutoffAsync(
        Guid businessId, Guid userId, int hours, CancellationToken ct = default)
    {
        if (hours is < 0 or > 720) // 0 h … 30 días
            throw new InvalidCancellationCutoffException(hours);

        var business = await repository.GetByIdAsync(businessId, ct)
            ?? throw new BusinessNotFoundException(businessId);
        if (business.OwnerId != userId)
            throw new NotBusinessOwnerException();

        business.CancellationCutoffHours = hours;
        await repository.UpdateAsync(business, ct);
        return BusinessResponse.From(business);
    }

    /// <summary>
    /// Cambia el plan del negocio ('free'|'premium'). Solo el owner. En el TFM es un
    /// upgrade simulado (sin pago); en producción lo invocará el webhook de la pasarela.
    /// </summary>
    public async Task<BusinessResponse> ChangePlanAsync(
        Guid businessId, Guid userId, string code, CancellationToken ct = default)
    {
        if (!ValidPlanCodes.Contains(code))
            throw new InvalidPlanException(code);

        var business = await repository.GetByIdAsync(businessId, ct)
            ?? throw new BusinessNotFoundException(businessId);
        if (business.OwnerId != userId)
            throw new NotBusinessOwnerException();

        var tier = await tiers.GetByCodeAsync(code, ct);
        business.TierId = tier.Id;
        await repository.UpdateAsync(business, ct);

        return BusinessResponse.From(business, tier.Code);
    }
}
