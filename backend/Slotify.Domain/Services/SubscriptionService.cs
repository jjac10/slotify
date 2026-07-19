using System.Security.Cryptography;
using System.Text;
using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Suscripción Premium con pago (cierra el TODO "gatear tras pago real"): el owner
/// abre un checkout (Stripe real o simulado, según <see cref="IPaymentGateway"/>) que
/// deja la suscripción 'pending'; al confirmarse el pago (webhook / endpoint simulado)
/// se activa y sube el plan. El downgrade a Free cancela la suscripción activa.
/// </summary>
public class SubscriptionService(
    ISubscriptionRepository subscriptions,
    IPaymentGateway gateway,
    IBusinessRepository businesses,
    ITierRepository tiers,
    BusinessService businessService)
{
    /// <summary>Abre el checkout del upgrade (solo el owner, solo si aún no es Premium).</summary>
    public async Task<string> StartCheckoutAsync(Guid businessId, Guid userId, CancellationToken ct = default)
    {
        var business = await businesses.GetByIdAsync(businessId, ct)
            ?? throw new BusinessNotFoundException(businessId);
        if (business.OwnerId != userId)
            throw new NotBusinessOwnerException();

        var premium = await tiers.GetByCodeAsync("premium", ct);
        if (business.TierId == premium.Id)
            throw new AlreadyPremiumException();

        var (externalId, url) = await gateway.CreateCheckoutAsync(businessId, business.Name, ct);
        await subscriptions.AddAsync(new Subscription
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Provider = gateway.Provider,
            ExternalId = externalId,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
        }, ct);

        return url;
    }

    /// <summary>
    /// El proveedor confirmó el pago: activa la suscripción y sube el plan. Idempotente
    /// (los webhooks se reintentan); una suscripción cancelada no se reactiva.
    /// </summary>
    public async Task CompleteCheckoutAsync(string externalId, CancellationToken ct = default)
    {
        var subscription = await subscriptions.GetByExternalIdAsync(externalId, ct)
            ?? throw new CheckoutNotFoundException();

        if (subscription.Status == "active")
            return; // reintento del webhook: ya está hecho
        if (subscription.Status == "cancelled")
            throw new CheckoutNotFoundException();

        subscription.Status = "active";
        subscription.ActivatedAt = DateTime.UtcNow;
        await subscriptions.UpdateAsync(subscription, ct);

        await businessService.SetPlanAsync(subscription.BusinessId, "premium", ct);
    }

    /// <summary>Baja a Free (solo el owner) y cancela la suscripción activa si la hay.</summary>
    public async Task<BusinessResponse> DowngradeAsync(Guid businessId, Guid userId, CancellationToken ct = default)
    {
        var response = await businessService.ChangePlanAsync(businessId, userId, "free", ct);

        var active = await subscriptions.GetActiveByBusinessAsync(businessId, ct);
        if (active is not null)
        {
            active.Status = "cancelled";
            active.CancelledAt = DateTime.UtcNow;
            await subscriptions.UpdateAsync(active, ct);
        }
        return response;
    }
}

/// <summary>
/// Verificación de la cabecera <c>Stripe-Signature</c> de los webhooks:
/// <c>t={unix},v1={HMAC-SHA256(secret, "{t}.{payload}")}</c>, con tolerancia de 5
/// minutos contra replays. Implementación propia (sin SDK) — misma mecánica que
/// documenta Stripe.
/// </summary>
public static class StripeWebhookVerifier
{
    private static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(5);

    public static bool Verify(string payload, string? signatureHeader, string secret, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        long? timestamp = null;
        string? v1 = null;
        foreach (var part in signatureHeader.Split(','))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0] == "t" && long.TryParse(kv[1], out var t)) timestamp = t;
            if (kv[0] == "v1") v1 = kv[1];
        }
        if (timestamp is null || string.IsNullOrEmpty(v1))
            return false;

        // Anti-replay: la firma caduca.
        var signedAt = DateTimeOffset.FromUnixTimeSeconds(timestamp.Value);
        if ((nowUtc - signedAt).Duration() > Tolerance)
            return false;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}")));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(v1.ToLowerInvariant()));
    }
}
