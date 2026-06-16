using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>Emisión de tokens de autenticación.</summary>
public interface ITokenService
{
    /// <summary>Genera un JWT de acceso firmado para el usuario.</summary>
    string CreateAccessToken(User user);

    /// <summary>Genera un refresh token opaco (valor aleatorio).</summary>
    string CreateRefreshToken();
}
