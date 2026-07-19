using Slotify.Domain.Entities;

namespace Slotify.Domain.Interfaces;

/// <summary>
/// Pasarela de pago del upgrade a Premium, swappable como el resto de la fontanería:
/// Stripe Checkout real si hay claves (STRIPE_*) y un checkout simulado si no (el
/// mismo flujo de redirección, sin cobro — demoable de punta a punta).
/// </summary>
public interface IPaymentGateway
{
    /// <summary>'stripe' | 'simulated'.</summary>
    string Provider { get; }

    /// <summary>
    /// Crea la sesión de pago del negocio y devuelve su id externo y la URL a la que
    /// mandar al owner (Stripe Checkout o el endpoint simulado).
    /// </summary>
    Task<(string ExternalId, string CheckoutUrl)> CreateCheckoutAsync(
        Guid businessId, string businessName, CancellationToken ct = default);
}

public interface ISubscriptionRepository
{
    Task AddAsync(Subscription subscription, CancellationToken ct = default);

    /// <summary>Suscripción por id externo (session de Stripe o token simulado), o null.</summary>
    Task<Subscription?> GetByExternalIdAsync(string externalId, CancellationToken ct = default);

    /// <summary>La suscripción activa del negocio, o null.</summary>
    Task<Subscription?> GetActiveByBusinessAsync(Guid businessId, CancellationToken ct = default);

    Task UpdateAsync(Subscription subscription, CancellationToken ct = default);
}
