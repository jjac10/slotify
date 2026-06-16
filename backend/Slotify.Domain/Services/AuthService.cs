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
    IRefreshTokenRepository refreshTokens)
{
    public const string FreeTierCode = "free";
    public const string OwnerType = "owner";
    public const string OwnerRole = "owner";

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
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

        await auth.RegisterOwnerAsync(user, business, ownerStaff, ct);

        return await IssueResultAsync(user, business.Id, ct);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await auth.GetByEmailAsync(request.Email, ct);
        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
            throw new InvalidCredentialsException();

        return await IssueResultAsync(user, businessId: null, ct);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var userId = await refreshTokens.ConsumeAsync(refreshToken, ct)
            ?? throw new InvalidRefreshTokenException();

        var user = await auth.GetByIdAsync(userId, ct)
            ?? throw new InvalidRefreshTokenException();

        return await IssueResultAsync(user, businessId: null, ct);
    }

    /// <summary>Emite access + refresh, persiste el refresh y devuelve el resultado.</summary>
    private async Task<AuthResult> IssueResultAsync(User user, Guid? businessId, CancellationToken ct)
    {
        var accessToken = tokens.CreateAccessToken(user);
        var refreshToken = tokens.CreateRefreshToken();
        await refreshTokens.IssueAsync(user.Id, refreshToken, ct);
        return new AuthResult(user.Id, businessId, accessToken, refreshToken);
    }
}
