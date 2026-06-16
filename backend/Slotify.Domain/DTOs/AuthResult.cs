namespace Slotify.Domain.DTOs;

/// <summary>Resultado de un registro/login: identidad creada + tokens emitidos.</summary>
public record AuthResult(Guid UserId, Guid? BusinessId, string AccessToken, string RefreshToken);
