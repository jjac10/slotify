using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Services;
using Slotify.Infrastructure.Notifications;
using Slotify.Infrastructure.Payments;

namespace Slotify.API.Controllers;

/// <summary>
/// Pago del plan Premium: abrir el checkout (Stripe real o simulado), el retorno del
/// checkout simulado y el webhook de Stripe. El upgrade de plan ya solo entra por aquí.
/// </summary>
[ApiController]
public class BillingController(
    SubscriptionService subscriptions,
    StripeOptions stripe,
    FrontendOptions frontend,
    ILogger<BillingController> logger) : ApiControllerBase
{
    /// <summary>Abre el checkout del upgrade a Premium (solo el owner). Devuelve la URL de pago.</summary>
    [HttpPost("/businesses/{businessId:guid}/checkout")]
    [Authorize]
    public async Task<ActionResult<object>> StartCheckout(Guid businessId, CancellationToken ct)
    {
        var url = await subscriptions.StartCheckoutAsync(businessId, CurrentUserId, ct);
        return Ok(new { url });
    }

    /// <summary>
    /// Retorno del checkout SIMULADO (sin claves de Stripe): "cobra" al instante y
    /// vuelve a Configuración. Token de un solo uso; desconocido → 404.
    /// </summary>
    [HttpGet("/checkout/simulated/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> CompleteSimulated(string token, CancellationToken ct)
    {
        await subscriptions.CompleteCheckoutAsync(token, ct);
        return Redirect($"{frontend.BaseUrl.TrimEnd('/')}/configuracion?upgraded=1");
    }

    /// <summary>
    /// Webhook de Stripe: verifica la firma (Stripe-Signature + STRIPE_WEBHOOK_SECRET)
    /// y activa la suscripción en checkout.session.completed. Otros eventos → 200 y se
    /// ignoran (Stripe reintenta los no-2xx).
    /// </summary>
    [HttpPost("/webhooks/stripe")]
    [AllowAnonymous]
    public async Task<IActionResult> StripeWebhook(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(stripe.WebhookSecret))
            return NotFound(); // sin Stripe configurado no existe el webhook

        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        if (!StripeWebhookVerifier.Verify(payload, signature, stripe.WebhookSecret, DateTimeOffset.UtcNow))
            return BadRequest(new { error = "invalid_signature", message = "La firma del webhook no es válida." });

        using var json = JsonDocument.Parse(payload);
        var type = json.RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;
        if (type != "checkout.session.completed")
            return Ok(); // evento que no nos interesa

        var sessionId = json.RootElement.GetProperty("data").GetProperty("object").GetProperty("id").GetString();
        try
        {
            await subscriptions.CompleteCheckoutAsync(sessionId!, ct);
        }
        catch (CheckoutNotFoundException)
        {
            // Sesión desconocida (p. ej. de otro entorno): lo registramos y respondemos
            // 200 para que Stripe no reintente eternamente.
            logger.LogWarning("Webhook de Stripe con checkout desconocido: {SessionId}", sessionId);
        }
        return Ok();
    }
}
