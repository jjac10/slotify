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

    /// <summary>Tamaño de página por defecto del listado público.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Tamaño de página máximo del listado público (protege la BD de peticiones absurdas).</summary>
    public const int MaxPageSize = 50;

    /// <summary>
    /// Listado/búsqueda pública de negocios (nombre + categoría opcional), paginado en BD.
    /// Clampa los valores absurdos: <paramref name="page"/> &lt; 1 → 1;
    /// <paramref name="pageSize"/> ≤ 0 → 20 (default); &gt; 50 → 50.
    /// </summary>
    public async Task<PagedResponse<BusinessResponse>> SearchPublicAsync(
        string? query, string? category = null, int page = 1, int pageSize = DefaultPageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        var (items, total) = await repository.SearchPublicAsync(query, category, (page - 1) * pageSize, pageSize, ct);
        return new PagedResponse<BusinessResponse>(items.Select(BusinessResponse.From).ToList(), total, page, pageSize);
    }

    /// <summary>Longitud máxima de la descripción del perfil público.</summary>
    public const int MaxDescriptionLength = 500;

    /// <summary>
    /// Actualiza el perfil público del negocio (categoría/foto/ubicación/contacto y
    /// personalización: descripción, web e Instagram). Solo el owner.
    /// </summary>
    public async Task<BusinessResponse> UpdateProfileAsync(
        Guid businessId, Guid userId, UpdateBusinessProfileRequest request, CancellationToken ct = default)
    {
        if (request.Category is { } cat && !BusinessCategories.IsValid(cat))
            throw new InvalidCategoryException(cat);

        var description = Normalize(request.Description);
        if (description is { Length: > MaxDescriptionLength })
            throw new InvalidBusinessProfileException($"La descripción no puede superar los {MaxDescriptionLength} caracteres.");

        // Web: solo http/https absolutas (nada de javascript: ni rutas sueltas).
        var website = Normalize(request.Website);
        if (website is not null &&
            (!Uri.TryCreate(website, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            throw new InvalidBusinessProfileException("La web debe ser una URL http(s) completa (https://…).");

        // Instagram: se guarda el usuario sin la '@' inicial.
        var instagram = Normalize(request.Instagram)?.TrimStart('@');
        if (instagram is { Length: > 100 })
            throw new InvalidBusinessProfileException("El usuario de Instagram no puede superar los 100 caracteres.");

        var business = await repository.GetByIdAsync(businessId, ct)
            ?? throw new BusinessNotFoundException(businessId);
        if (business.OwnerId != userId)
            throw new NotBusinessOwnerException();

        business.Category = request.Category;
        business.PhotoUrl = request.PhotoUrl;
        business.Latitude = request.Latitude;
        business.Longitude = request.Longitude;
        business.Phone = Normalize(request.Phone);
        business.Address = Normalize(request.Address);
        business.Description = description;
        business.Website = website;
        business.Instagram = string.IsNullOrEmpty(instagram) ? null : instagram;
        await repository.UpdateAsync(business, ct);

        return BusinessResponse.From(business);
    }

    /// <summary>Trim; en blanco → null (campo sin valor).</summary>
    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
    /// Cambia el modo de reservas del negocio ('online'|'calendar_only'). Solo el owner.
    /// En 'calendar_only' el negocio no acepta reservas online ni sale en Explorar.
    /// </summary>
    public async Task<BusinessResponse> SetBookingModeAsync(
        Guid businessId, Guid userId, string mode, CancellationToken ct = default)
    {
        if (mode is not ("online" or "calendar_only"))
            throw new InvalidBookingModeException(mode);

        var business = await repository.GetByIdAsync(businessId, ct)
            ?? throw new BusinessNotFoundException(businessId);
        if (business.OwnerId != userId)
            throw new NotBusinessOwnerException();

        business.BookingMode = mode;
        await repository.UpdateAsync(business, ct);
        return BusinessResponse.From(business);
    }

    /// <summary>
    /// Configura los avisos del negocio (canales email/WhatsApp + antelación del
    /// recordatorio en horas, 0 = sin recordatorio). Solo el owner.
    /// </summary>
    public async Task<BusinessResponse> SetNotificationSettingsAsync(
        Guid businessId, Guid userId, SetNotificationSettingsRequest request, CancellationToken ct = default)
    {
        if (request.ReminderHoursBefore is < 0 or > 168) // 0 h … 7 días
            throw new InvalidNotificationSettingsException("La antelación del recordatorio debe estar entre 0 y 168 horas.");

        var business = await repository.GetByIdAsync(businessId, ct)
            ?? throw new BusinessNotFoundException(businessId);
        if (business.OwnerId != userId)
            throw new NotBusinessOwnerException();

        business.NotifyByEmail = request.NotifyByEmail;
        business.NotifyByWhatsapp = request.NotifyByWhatsapp;
        business.ReminderHoursBefore = request.ReminderHoursBefore;
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
