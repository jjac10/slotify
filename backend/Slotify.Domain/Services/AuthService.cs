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
    IBusinessRepository businesses,
    IStaffRepository staff)
{
    public const string FreeTierCode = "free";
    public const string OwnerType = "owner";
    public const string CustomerType = "customer";
    public const string EmployeeType = "employee";
    public const string OwnerRole = "owner";
    public const string StaffRole = "staff";

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

        return await IssueResultAsync(user, businessId: null, role: null, ct);
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

        return await IssueResultAsync(user, business.Id, OwnerRole, ct);
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

        var (businessId, role) = await ResolveMembershipAsync(user, ct);
        return await IssueResultAsync(user, businessId, role, ct);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var userId = await refreshTokens.ConsumeAsync(refreshToken, ct)
            ?? throw new InvalidRefreshTokenException();

        var user = await auth.GetByIdAsync(userId, ct)
            ?? throw new InvalidRefreshTokenException();

        var (businessId, role) = await ResolveMembershipAsync(user, ct);
        return await IssueResultAsync(user, businessId, role, ct);
    }

    /// <summary>
    /// Un empleado crea su cuenta a partir del token de invitación: alta de user
    /// (type='employee') enlazado a su registro de staff, e inicio de sesión.
    /// </summary>
    public async Task<AuthResult> AcceptStaffInviteAsync(string token, string password, CancellationToken ct = default)
    {
        PasswordPolicy.Validate(password);

        var member = await staff.GetByInviteTokenAsync(token, ct);
        if (member is null || member.UserId is not null || string.IsNullOrWhiteSpace(member.Email))
            throw new StaffInviteNotFoundException();
        if (await auth.EmailExistsAsync(member.Email, ct))
            throw new EmailAlreadyExistsException(member.Email);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = member.Email,
            PasswordHash = hasher.Hash(password),
            Name = member.Name,
            Type = EmployeeType,
        };
        await auth.AddUserAsync(user, ct);

        member.UserId = user.Id;
        member.InviteToken = null; // la invitación se consume
        await staff.UpdateAsync(member, ct);

        return await IssueResultAsync(user, member.BusinessId, StaffRole, ct);
    }

    /// <summary>Datos de una invitación pendiente (para la pantalla de aceptar), o error si no es válida.</summary>
    public async Task<StaffInviteInfoResponse> GetStaffInviteAsync(string token, CancellationToken ct = default)
    {
        var member = await staff.GetByInviteTokenAsync(token, ct);
        if (member is null || member.UserId is not null || string.IsNullOrWhiteSpace(member.Email))
            throw new StaffInviteNotFoundException();
        return new StaffInviteInfoResponse(member.Business?.Name ?? "el negocio", member.Name, member.Email!);
    }

    /// <summary>Emite access + refresh, persiste el refresh y devuelve el resultado.</summary>
    private async Task<AuthResult> IssueResultAsync(User user, Guid? businessId, string? role, CancellationToken ct)
    {
        var accessToken = tokens.CreateAccessToken(user);
        var refreshToken = tokens.CreateRefreshToken();
        await refreshTokens.IssueAsync(user.Id, refreshToken, ct);
        return new AuthResult(user.Id, businessId, accessToken, refreshToken, role);
    }

    /// <summary>
    /// Pertenencia del usuario a un negocio (para login/refresh): si es owner, su negocio
    /// con rol 'owner'; si es un empleado con cuenta, el negocio de su staff con rol 'staff';
    /// los clientes no pertenecen a ninguno.
    /// </summary>
    private async Task<(Guid? businessId, string? role)> ResolveMembershipAsync(User user, CancellationToken ct)
    {
        if (user.Type == OwnerType)
        {
            var owned = await businesses.ListByOwnerAsync(user.Id, ct);
            if (owned.Count > 0)
                return (owned[0].Id, OwnerRole);
        }

        var membership = await staff.GetByUserAsync(user.Id, ct);
        if (membership is not null && membership.Role != OwnerRole)
            return (membership.BusinessId, StaffRole);

        return (null, null);
    }
}
