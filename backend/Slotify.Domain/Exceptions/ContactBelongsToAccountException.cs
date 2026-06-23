namespace Slotify.Domain.Exceptions;

/// <summary>
/// El teléfono/email indicado en una reserva de invitado pertenece a una cuenta
/// registrada. No se permite reservar "como" esa cuenta sin iniciar sesión (evita la
/// suplantación): el dueño del contacto debe loguearse. El owner/staff sí puede
/// apuntar la reserva a la cuenta del cliente desde su agenda. HTTP 409.
/// </summary>
public class ContactBelongsToAccountException()
    : Exception("Ese teléfono o email ya tiene una cuenta. Inicia sesión para reservar con él.");
