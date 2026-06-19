namespace Slotify.Domain.DTOs;

/// <summary>
/// Cambia el plan (tier) del negocio por su código estable ('free'|'premium').
/// Solo el owner. En el TFM el upgrade es simulado (sin pago); en producción lo
/// disparará el webhook de la pasarela de pago.
/// </summary>
public record SetPlanRequest(string Code);
