namespace Slotify.Domain.Exceptions;

/// <summary>Se alcanzó el límite del plan Freemium para este recurso (ADR #9).</summary>
public class FreemiumLimitReachedException(string resource)
    : Exception($"Has alcanzado el límite de tu plan para: {resource}. Mejora a Premium para más.");
