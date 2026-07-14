namespace Slotify.Domain.Exceptions;

/// <summary>Aún quedan huecos libres ese día: se reserva, no se hace cola. HTTP 409.</summary>
public class WaitlistNotNeededException()
    : Exception("Todavía quedan huecos libres ese día: reserva directamente.");

/// <summary>El usuario ya está en la lista de espera de ese servicio y día. HTTP 409.</summary>
public class AlreadyOnWaitlistException()
    : Exception("Ya estás en la lista de espera de ese día.");

/// <summary>Solo tiene sentido esperar para hoy o el futuro. HTTP 400.</summary>
public class InvalidWaitlistDateException()
    : Exception("La fecha de la lista de espera debe ser hoy o futura.");

/// <summary>La entrada de lista de espera no existe. HTTP 404.</summary>
public class WaitlistEntryNotFoundException(Guid id)
    : Exception($"La entrada de lista de espera {id} no existe.");
