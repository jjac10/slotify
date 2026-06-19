namespace Slotify.Domain.DTOs;

/// <summary>
/// Cambia cómo se confirman las reservas nuevas del negocio: <c>auto</c> (se confirman
/// solas) o <c>manual</c> (el owner/staff las confirma). Solo el owner.
/// </summary>
public record SetConfirmationModeRequest(string Mode);
