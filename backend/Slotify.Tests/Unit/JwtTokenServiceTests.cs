using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Slotify.Domain.Entities;
using Slotify.Infrastructure.Security;

namespace Slotify.Tests.Unit;

/// <summary>Emisión de JWT de acceso (HS256, ADR #3).</summary>
public class JwtTokenServiceTests
{
    private readonly JwtOptions _options = new()
    {
        Key = "unit-test-secret-key-must-be-at-least-32-bytes!!",
        Issuer = "slotify",
        Audience = "slotify-clients",
        AccessTokenMinutes = 60,
        RefreshTokenDays = 7,
    };

    [Fact]
    public void CreateAccessToken_IsValid_AndCarriesSubAndEmail()
    {
        var service = new JwtTokenService(_options);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "owner@example.com",
            Name = "Owner",
            Type = "owner",
        };

        var token = service.CreateAccessToken(user);

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
            ClockSkew = TimeSpan.Zero,
        };

        // MapInboundClaims=false: conserva los nombres originales del JWT (sub, email)
        // en lugar de remapearlos a las URIs largas de ClaimTypes.
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(token, parameters, out _);

        Assert.Equal(user.Id.ToString(), principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
        Assert.Equal(user.Email, principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value);
    }
}
