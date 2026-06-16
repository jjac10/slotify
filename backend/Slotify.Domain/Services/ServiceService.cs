using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Gestión de servicios de un negocio. El alta valida propiedad (solo el owner)
/// y el límite Freemium del plan (ADR #9). El listado es público.
/// </summary>
public class ServiceService(
    IServiceRepository services,
    IBusinessRepository businesses,
    IFreemiumLimitService limits)
{
    public async Task<ServiceResponse> CreateAsync(
        Guid businessId, Guid currentUserId, CreateServiceRequest request, CancellationToken ct = default)
    {
        var business = await businesses.GetByIdAsync(businessId, ct)
            ?? throw new BusinessNotFoundException(businessId);

        if (business.OwnerId != currentUserId)
            throw new NotBusinessOwnerException();

        if (!await limits.CanAddServiceAsync(businessId, ct))
            throw new FreemiumLimitReachedException("servicios");

        var service = new Service
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Name = request.Name,
            Description = request.Description,
            DurationMinutes = request.DurationMinutes,
            Price = request.Price,
            Color = request.Color,
        };
        await services.AddAsync(service, ct);

        return ServiceResponse.From(service);
    }

    public async Task<IReadOnlyList<ServiceResponse>> ListAsync(Guid businessId, CancellationToken ct = default)
    {
        var list = await services.ListByBusinessAsync(businessId, ct);
        return list.Select(ServiceResponse.From).ToList();
    }
}
