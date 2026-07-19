using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Notifications;

namespace Slotify.Infrastructure.Payments;

/// <summary>
/// Configuración de Stripe desde variables de entorno (<c>STRIPE_SECRET_KEY</c>,
/// <c>STRIPE_PRICE_ID</c>, <c>STRIPE_WEBHOOK_SECRET</c>). Sin ellas (desarrollo/demo),
/// el checkout es simulado: mismo flujo de redirección, sin cobro.
/// </summary>
public class StripeOptions
{
    public string? SecretKey { get; set; }

    /// <summary>Price recurrente del plan Premium (price_… del dashboard de Stripe).</summary>
    public string? PriceId { get; set; }

    /// <summary>Secreto de firma del webhook (whsec_…).</summary>
    public string? WebhookSecret { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SecretKey) && !string.IsNullOrWhiteSpace(PriceId);

    public static StripeOptions FromConfiguration(IConfiguration configuration) => new()
    {
        SecretKey = configuration["STRIPE_SECRET_KEY"],
        PriceId = configuration["STRIPE_PRICE_ID"],
        WebhookSecret = configuration["STRIPE_WEBHOOK_SECRET"],
    };
}

/// <summary>
/// Checkout simulado (sin claves de Stripe): genera un token y manda al owner a
/// GET /api/checkout/simulated/{token}, que "cobra" al instante y vuelve a
/// Configuración. Mismo recorrido que el real, sin pasarela.
/// </summary>
public class SimulatedPaymentGateway : IPaymentGateway
{
    public string Provider => "simulated";

    public Task<(string ExternalId, string CheckoutUrl)> CreateCheckoutAsync(
        Guid businessId, string businessName, CancellationToken ct = default)
    {
        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
        // Relativa al origen del frontend: el proxy /api la lleva al backend.
        return Task.FromResult((token, $"/api/checkout/simulated/{token}"));
    }
}

/// <summary>
/// Stripe Checkout real (suscripción con el Price configurado), vía HTTP directo
/// (misma mecánica ligera que el transporte de Twilio: sin SDK). El pago se confirma
/// por el webhook checkout.session.completed.
/// </summary>
public class StripePaymentGateway(
    HttpClient httpClient,
    StripeOptions options,
    FrontendOptions frontend) : IPaymentGateway
{
    public string Provider => "stripe";

    public async Task<(string ExternalId, string CheckoutUrl)> CreateCheckoutAsync(
        Guid businessId, string businessName, CancellationToken ct = default)
    {
        if (!options.IsConfigured)
            throw new InvalidOperationException("Stripe sin configurar (faltan STRIPE_SECRET_KEY/STRIPE_PRICE_ID).");

        var baseUrl = frontend.BaseUrl.TrimEnd('/');
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/checkout/sessions")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["mode"] = "subscription",
                ["line_items[0][price]"] = options.PriceId!,
                ["line_items[0][quantity]"] = "1",
                ["success_url"] = $"{baseUrl}/configuracion?upgraded=1",
                ["cancel_url"] = $"{baseUrl}/configuracion",
                ["client_reference_id"] = businessId.ToString(),
                ["metadata[businessId]"] = businessId.ToString(),
                ["metadata[businessName]"] = businessName,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.SecretKey);

        var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var id = json.RootElement.GetProperty("id").GetString()!;
        var url = json.RootElement.GetProperty("url").GetString()!;
        return (id, url);
    }
}
