using Slotify.Domain.DTOs;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// El rol de admin de plataforma no vive en BD: es el email configurado en
/// <c>Admin:Email</c> (o la variable de entorno <c>Admin__Email</c>). Sin
/// configurar → no hay admin (los endpoints /admin devuelven 403 a todos).
/// </summary>
public class AdminOptions
{
    public string? Email { get; set; }

    public bool IsAdmin(string? email) =>
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(email) &&
        string.Equals(Email.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Borrado de un negocio (RGPD: hard-delete en cascada de todos sus datos) y
/// moderación de la plataforma:
/// - Owner: "confirmación máxima" — escribir el nombre exacto del negocio y la
///   contraseña actual; bloqueado si hay reservas futuras activas (409).
/// - Admin: borra negocios spam/prueba sin esas confirmaciones (la autorización por
///   rol la hace el endpoint con <see cref="AdminOptions"/>), incluso con reservas.
/// </summary>
public class BusinessDeletionService(
    IAuthRepository auth,
    IPasswordHasher hasher,
    IBusinessRepository businesses,
    IBusinessDeletionRepository deletion)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    public async Task DeleteAsOwnerAsync(
        Guid businessId, Guid currentUserId, string? confirmName, string? password, CancellationToken ct = default)
    {
        var business = await businesses.GetByIdAsync(businessId, ct)
            ?? throw new BusinessNotFoundException(businessId);

        var user = await auth.GetByIdAsync(currentUserId, ct)
            ?? throw new InvalidCredentialsException();

        // Confirmación con la contraseña actual (igual que valida el login).
        if (string.IsNullOrEmpty(password) || !hasher.Verify(password, user.PasswordHash))
            throw new InvalidCredentialsException();

        if (business.OwnerId != currentUserId)
            throw new NotBusinessOwnerException();

        // Confirmación máxima: el nombre exacto del negocio, tal cual (solo se toleran
        // espacios alrededor).
        if (confirmName?.Trim() != business.Name)
            throw new BusinessNameMismatchException();

        // Con citas futuras activas no se borra: primero hay que cancelarlas/avisarlas
        // (mismo criterio que el borrado de cuenta del owner).
        if (await deletion.HasFutureActiveReservationsAsync(businessId, DateTime.UtcNow, ct))
            throw new BusinessHasFutureReservationsException();

        await deletion.DeleteBusinessCascadeAsync(businessId, ct);
    }

    public async Task DeleteAsAdminAsync(Guid businessId, CancellationToken ct = default)
    {
        _ = await businesses.GetByIdAsync(businessId, ct)
            ?? throw new BusinessNotFoundException(businessId);

        // Moderación: el admin puede retirar un negocio aunque tenga reservas futuras
        // (spam/prueba); el cascade las elimina.
        await deletion.DeleteBusinessCascadeAsync(businessId, ct);
    }

    /// <summary>Directorio de moderación: todos los negocios con el email de su owner.</summary>
    public async Task<PagedResponse<AdminBusinessResponse>> ListForAdminAsync(
        string? q, int page = 1, int pageSize = DefaultPageSize, CancellationToken ct = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize)
            throw new InvalidPaginationException(page, pageSize, MaxPageSize);

        var (items, total) = await deletion.ListForAdminAsync(q, (page - 1) * pageSize, pageSize, ct);
        var mapped = items
            .Select(i => new AdminBusinessResponse(i.Business.Id, i.Business.Name, i.OwnerEmail, i.Business.CreatedAt))
            .ToList();
        return new PagedResponse<AdminBusinessResponse>(mapped, total, page, pageSize);
    }
}
