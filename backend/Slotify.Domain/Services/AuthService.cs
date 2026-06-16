using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Autenticación: registro de propietario (RF-AUTH-001), login (RF-AUTH-002).
/// El plan vive en el negocio (businesses.tier_id); el registro crea el negocio
/// en plan Free y su owner-as-staff.
/// </summary>
public class AuthService(
    IAuthRepository auth,
    ITierRepository tiers,
    IPasswordHasher hasher,
    ITokenService tokens)
{
    public const string FreeTierCode = "free";
    public const string OwnerType = "owner";
    public const string OwnerRole = "owner";

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
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

        return new AuthResult(
            user.Id,
            business.Id,
            tokens.CreateAccessToken(user),
            tokens.CreateRefreshToken());
    }
}
