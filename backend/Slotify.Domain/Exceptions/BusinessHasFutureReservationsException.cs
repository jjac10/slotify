namespace Slotify.Domain.Exceptions;

/// <summary>
/// El owner no puede borrar su cuenta mientras su negocio tenga reservas futuras
/// activas de clientes: debe cancelarlas o traspasar el negocio primero (→ 409).
/// </summary>
public class BusinessHasFutureReservationsException() : Exception(
    "Tu negocio tiene reservas futuras de clientes. Cancélalas o traspasa el negocio antes de eliminar tu cuenta.");
