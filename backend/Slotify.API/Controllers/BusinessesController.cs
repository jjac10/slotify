using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Slotify.Domain.DTOs;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Services;

namespace Slotify.API.Controllers;

[ApiController]
[Route("businesses")]
public class BusinessesController(
    BusinessService businesses,
    BusinessDeletionService businessDeletion,
    SubscriptionService subscriptions,
    BusinessPhotoService photos) : ApiControllerBase
{
    /// <summary>Lista los negocios del owner autenticado.</summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<BusinessResponse>>> ListMine(CancellationToken ct)
        => Ok(await businesses.ListByOwnerAsync(CurrentUserId, ct));

    /// <summary>
    /// Sube la foto del negocio (multipart, campo 'photo'; JPG/PNG/WebP, máx. 5 MB).
    /// Solo el owner. La URL pública resultante queda en photo_url.
    /// </summary>
    [HttpPost("{id:guid}/photo")]
    [Authorize]
    [RequestSizeLimit(BusinessPhotoService.MaxBytes + 1024 * 1024)] // margen para el overhead multipart
    public Task<ActionResult<BusinessResponse>> UploadPhoto(Guid id, IFormFile? photo, CancellationToken ct)
        => UploadImage(id, photo, BusinessImageKind.Photo, ct);

    /// <summary>Sube el logo del negocio (multipart, campo 'photo'). Igual que la foto, en logo_url.</summary>
    [HttpPost("{id:guid}/logo")]
    [Authorize]
    [RequestSizeLimit(BusinessPhotoService.MaxBytes + 1024 * 1024)]
    public Task<ActionResult<BusinessResponse>> UploadLogo(Guid id, IFormFile? photo, CancellationToken ct)
        => UploadImage(id, photo, BusinessImageKind.Logo, ct);

    private async Task<ActionResult<BusinessResponse>> UploadImage(
        Guid id, IFormFile? photo, BusinessImageKind kind, CancellationToken ct)
    {
        if (photo is null || photo.Length == 0)
            return BadRequest(new { error = "invalid_photo", message = "Falta la imagen (campo 'photo')." });

        await using var content = photo.OpenReadStream();
        return Ok(await photos.UploadAsync(id, CurrentUserId, content, photo.ContentType, photo.Length, kind, ct));
    }

    /// <summary>
    /// Elimina el negocio y TODOS sus datos en cascada (RGPD). Confirmación máxima:
    /// el owner debe escribir el nombre exacto del negocio y su contraseña actual.
    /// Bloqueado si hay reservas futuras activas (409): primero hay que cancelarlas.
    /// </summary>
    [HttpPost("{id:guid}/delete")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id, DeleteBusinessRequest request, CancellationToken ct)
    {
        try
        {
            await businessDeletion.DeleteAsOwnerAsync(id, CurrentUserId, request.Name, request.Password, ct);
            return NoContent();
        }
        catch (InvalidCredentialsException)
        {
            return BadRequest(new { error = "invalid_password", message = "La contraseña no es correcta." });
        }
    }

    /// <summary>Cambia el modo de confirmación de reservas del negocio ('auto'|'manual'). Solo el owner.</summary>
    [HttpPut("{id:guid}/confirmation-mode")]
    [Authorize]
    public async Task<ActionResult<BusinessResponse>> SetConfirmationMode(
        Guid id, SetConfirmationModeRequest request, CancellationToken ct)
    {
        return Ok(await businesses.SetConfirmationModeAsync(id, CurrentUserId, request.Mode, ct));
    }

    /// <summary>Fija la antelación mínima (horas) para que el cliente cancele/reprograme. 0 = sin límite. Solo el owner.</summary>
    [HttpPut("{id:guid}/cancellation-cutoff")]
    [Authorize]
    public async Task<ActionResult<BusinessResponse>> SetCancellationCutoff(
        Guid id, SetCancellationCutoffRequest request, CancellationToken ct)
    {
        return Ok(await businesses.SetCancellationCutoffAsync(id, CurrentUserId, request.Hours, ct));
    }

    /// <summary>Configura los avisos del negocio (canales email/WhatsApp + recordatorio). Solo el owner.</summary>
    [HttpPut("{id:guid}/notification-settings")]
    [Authorize]
    public async Task<ActionResult<BusinessResponse>> SetNotificationSettings(
        Guid id, SetNotificationSettingsRequest request, CancellationToken ct)
    {
        return Ok(await businesses.SetNotificationSettingsAsync(id, CurrentUserId, request, ct));
    }

    /// <summary>Cambia el modo de reservas ('online'|'calendar_only'). Solo el owner.</summary>
    [HttpPut("{id:guid}/booking-mode")]
    [Authorize]
    public async Task<ActionResult<BusinessResponse>> SetBookingMode(
        Guid id, SetBookingModeRequest request, CancellationToken ct)
    {
        return Ok(await businesses.SetBookingModeAsync(id, CurrentUserId, request.Mode, ct));
    }

    /// <summary>
    /// Cambia el plan del negocio. Solo el owner, y solo hacia 'free' (cancela la
    /// suscripción activa); el upgrade a Premium pasa por el checkout de pago (409).
    /// </summary>
    [HttpPut("{id:guid}/plan")]
    [Authorize]
    public async Task<ActionResult<BusinessResponse>> ChangePlan(
        Guid id, SetPlanRequest request, CancellationToken ct)
    {
        return Ok(request.Code == "free"
            ? await subscriptions.DowngradeAsync(id, CurrentUserId, ct)
            : await businesses.ChangePlanAsync(id, CurrentUserId, request.Code, ct));
    }

    /// <summary>Actualiza el perfil público del negocio (categoría/foto/ubicación). Solo el owner.</summary>
    [HttpPut("{id:guid}/profile")]
    [Authorize]
    public async Task<ActionResult<BusinessResponse>> UpdateProfile(
        Guid id, UpdateBusinessProfileRequest request, CancellationToken ct)
    {
        return Ok(await businesses.UpdateProfileAsync(id, CurrentUserId, request, ct));
    }

    /// <summary>
    /// Listado/búsqueda pública de negocios activos (para que un cliente elija dónde
    /// reservar). Filtro opcional por nombre con <c>?q=</c> y por categoría con <c>?category=</c>.
    /// Paginado en BD con <c>?page=</c> (1-based, default 1) y <c>?pageSize=</c> (default 20,
    /// máx. 50); responde <c>{ items, total, page, pageSize }</c>.
    /// </summary>
    [HttpGet("/public/businesses")]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResponse<BusinessResponse>>> SearchPublic(
        [FromQuery] string? q, [FromQuery] string? category,
        [FromQuery] int page = 1, [FromQuery] int pageSize = BusinessService.DefaultPageSize,
        CancellationToken ct = default)
        => Ok(await businesses.SearchPublicAsync(q, category, page, pageSize, ct));
}
