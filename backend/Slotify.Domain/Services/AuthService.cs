using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Autenticación: registro de propietario (RF-AUTH-001), login (RF-AUTH-002) y
/// renovación de tokens (refresh con rotación). El plan vive en el negocio
/// (businesses.tier_id); el registro crea el negocio Free y su owner-as-staff.
/// </summary>
public class AuthService(
    IAuthRepository auth,
    ITierRepository tiers,
    IPasswordHasher hasher,
    ITokenService tokens,
    IRefreshTokenRepository refreshTokens,
    IGuestRepository guests,
    IBlindIndex blindIndex,
    IBusinessRepository businesses)
{
    public const string FreeTierCode = "free";
    public const string OwnerType = "owner";
    public const string CustomerType = "customer";
    public const string OwnerRole = "owner";

    /// <summary>
    /// Alta de cliente (sin negocio): user con type='customer'. Además vincula
    /// automáticamente las reservas previas hechas como invitado que coincidan por
    /// email (y teléfono, si se indica) — sync invitado→usuario (DATA_MODEL).
    /// </summary>
    public async Task<AuthResult> RegisterCustomerAsync(RegisterCustomerRequest request, CancellationToken ct = default)
    {
        PasswordPolicy.Validate(request.Password);

        if (await auth.EmailExistsAsync(request.Email, ct))
            throw new EmailAlreadyExistsException(request.Email);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = hasher.Hash(request.Password),
            Name = request.Name,
            Phone = request.Phone,
            Type = CustomerType,
        };
        await auth.AddUserAsync(user, ct);

        // Sync invitado→usuario: enlaza guests por blind index (email + teléfono).
        var emailHash = blindIndex.Compute(ContactNormalizer.NormalizeEmail(request.Email));
        var phoneHash = string.IsNullOrWhiteSpace(request.Phone)
            ? null
            : blindIndex.Compute(ContactNormalizer.NormalizePhone(request.Phone));
        await guests.LinkToUserByHashAsync(user.Id, phoneHash, emailHash, ct);

        return await IssueResultAsync(user, businessId: null, ct);
    }

    /// <summary>Alta de propietario + su negocio (plan Free) + owner-staff, atómico.</summary>
    public async Task<AuthResult> RegisterOwnerAsync(RegisterOwnerRequest request, CancellationToken ct = default)
    {
        PasswordPolicy.Validate(request.Password); // rechaza contraseñas débiles antes de tocar BD

        if (await auth.EmailExistsAsync(request.Email, ct))
            throw new EmailAlreadyExistsException(request.Email);

        var freeTier = await tiers.GetByCodeAsync(FreeTierCode, ct);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = hasher.Hash(request.Password),
            Name = request.Name,
            Type = OwnerType,
        };

        var business = new Business
        {
            Id = Guid.NewGuid(),
            OwnerId = user.Id,
            TierId = freeTier.Id,
            Name = request.BusinessName,
        };

        var ownerStaff = new Staff
        {
            Id = Guid.NewGuid(),
            BusinessId = business.Id,
            UserId = user.Id,
            Role = OwnerRole,
            Name = request.Name,
        };

        await auth.RegisterOwnerAsync(user, business, ownerStaff, DefaultWeeklyHours(business.Id), ct);

        return await IssueResultAsync(user, business.Id, ct);
    }

    /// <summary>
    /// Horario semanal por defecto al crear un negocio: L–V abierto 09:00–17:00,
    /// sábado y domingo cerrados. El owner puede ajustarlo después. Así el negocio
    /// tiene disponibilidad desde el primer momento, sin un paso manual previo.
    /// </summary>
    private static IReadOnlyList<BusinessHour> DefaultWeeklyHours(Guid businessId)
    {
        var opening = new TimeOnly(9, 0);
        var closing = new TimeOnly(17, 0);
        return Enumerable.Range(0, 7).Select(day =>
        {
            var weekday = day is >= 1 and <= 5; // 0=domingo … 6=sábado
            return new BusinessHour
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                DayOfWeek = day,
                IsClosed = !weekday,
                OpeningTime = weekday ? opening : null,
                ClosingTime = weekday ? closing : null,
            };
        }).ToList();
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await auth.GetByEmailAsync(request.Email, ct);
        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
            throw new InvalidCredentialsException();

        return await IssueResultAsync(user, await ResolveOwnedBusinessIdAsync(user, ct), ct);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var userId = await refreshTokens.ConsumeAsync(refreshToken, ct)
            ?? throw new InvalidRefreshTokenException();

        var user = await auth.GetByIdAsync(userId, ct)
            ?? throw new InvalidRefreshTokenException();

        return await IssueResultAsync(user, await ResolveOwnedBusinessIdAsync(user, ct), ct);
    }

    /// <summary>Emite access + refresh, persiste el refresh y devuelve el resultado.</summary>
    private async Task<AuthResult> IssueResultAsync(User user, Guid? businessId, CancellationToken ct)
    {
        var accessToken = tokens.CreateAccessToken(user);
        var refreshToken = tokens.CreateRefreshToken();
        await refreshTokens.IssueAsync(user.Id, refreshToken, ct);
        return new AuthResult(user.Id, businessId, accessToken, refreshToken);
    }

    /// <summary>
    /// Negocio que posee el usuario (si es owner), para que el cliente sepa que tiene
    /// negocio al loguearse o renovar. Los clientes no consultan BD (no tienen negocio).
    /// </summary>
    private async Task<Guid?> ResolveOwnedBusinessIdAsync(User user, CancellationToken ct)
    {
        if (user.Type != OwnerType)
            return null;

        var owned = await businesses.ListByOwnerAsync(user.Id, ct);
        return owned.Count > 0 ? owned[0].Id : null;
    }
}
