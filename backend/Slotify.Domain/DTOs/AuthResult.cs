namespace Slotify.Domain.DTOs;

/// <summary>
/// Resultado de un registro/login: identidad creada + tokens emitidos. <c>BusinessId</c>
/// no nulo ⇒ el usuario pertenece a un negocio; <c>BusinessRole</c> distingue si es su
/// <c>owner</c> o un <c>staff</c> (empleado). NULL para clientes.
/// </summary>
public record AuthResult(Guid UserId, Guid? BusinessId, string AccessToken, string RefreshToken, string? BusinessRole = null);
