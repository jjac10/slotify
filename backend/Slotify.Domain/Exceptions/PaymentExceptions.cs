namespace Slotify.Domain.Exceptions;

/// <summary>El upgrade a Premium requiere pasar por el checkout de pago. HTTP 409.</summary>
public class PaymentRequiredException()
    : Exception("El plan Premium se activa desde el checkout de pago, no directamente.");

/// <summary>El negocio ya es Premium: no hay nada que pagar. HTTP 409.</summary>
public class AlreadyPremiumException()
    : Exception("El negocio ya tiene el plan Premium.");

/// <summary>La sesión de pago no existe o ya se usó. HTTP 404.</summary>
public class CheckoutNotFoundException()
    : Exception("La sesión de pago no existe o ya se completó.");
