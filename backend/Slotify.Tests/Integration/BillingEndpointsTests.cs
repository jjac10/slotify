using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// Pago del plan Premium end-to-end. Sin claves de Stripe el checkout es SIMULADO:
/// abrirlo crea la suscripción 'pending' y seguir su URL la activa y sube el plan; el
/// token es de un solo uso y el upgrade directo por PUT /plan devuelve 409
/// payment_required. El downgrade a Free cancela la suscripción. El webhook de Stripe
/// (con secreto configurado) activa por firma; firma inválida → 400.
/// </summary>
public class BillingEndpointsTests : IClassFixture<BillingEndpointsTests.WebhookFactory>
{
    private const string WebhookSecret = "whsec_test_integration";

    private readonly WebhookFactory _factory;
    private readonly HttpClient _client;

    public BillingEndpointsTests(WebhookFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Guid businessId, HttpClient owner)> RegisterBusinessAsync()
    {
        var req = new RegisterOwnerRequest($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería");
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner", req)).Content.ReadFromJsonAsync<AuthResult>();
        var owner = _factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (auth.BusinessId!.Value, owner);
    }

    private async Task<string> GetPlanAsync(HttpClient owner, Guid businessId)
    {
        var mine = await (await owner.GetAsync("/businesses")).Content.ReadFromJsonAsync<List<BusinessResponse>>();
        return mine!.Single(b => b.Id == businessId).Plan!;
    }

    [Fact]
    public async Task SimulatedCheckout_FullFlow_UpgradesPlan_TokenIsSingleUse()
    {
        var (businessId, owner) = await RegisterBusinessAsync();

        // Abrir checkout → URL simulada con token; la suscripción queda pending.
        var checkout = await owner.PostAsync($"/businesses/{businessId}/checkout", null);
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
        var url = (await checkout.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;
        Assert.Contains("/checkout/simulated/", url);
        var token = url.TrimEnd('/').Split('/')[^1];

        // Seguir la URL "paga" y redirige a Configuración con ?upgraded=1.
        using var raw = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var complete = await raw.GetAsync($"/checkout/simulated/{token}");
        Assert.Equal(HttpStatusCode.Redirect, complete.StatusCode);
        Assert.Contains("upgraded=1", complete.Headers.Location!.ToString());

        Assert.Equal("premium", await GetPlanAsync(owner, businessId));
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
            var sub = await db.Subscriptions.SingleAsync(s => s.BusinessId == businessId);
            Assert.Equal("active", sub.Status);
            Assert.Equal("simulated", sub.Provider);
        }

        // El token ya se usó… pero el retorno es idempotente (Stripe también reintenta).
        var again = await raw.GetAsync($"/checkout/simulated/{token}");
        Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);

        // Ya Premium: abrir otro checkout → 409.
        var second = await owner.PostAsync($"/businesses/{businessId}/checkout", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("already_premium", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DirectPlanUpgrade_Returns409PaymentRequired()
    {
        var (businessId, owner) = await RegisterBusinessAsync();

        var res = await owner.PutAsJsonAsync($"/businesses/{businessId}/plan", new SetPlanRequest("premium"));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("payment_required", await res.Content.ReadAsStringAsync());
        Assert.Equal("free", await GetPlanAsync(owner, businessId));
    }

    [Fact]
    public async Task Downgrade_ReturnsToFree_AndCancelsSubscription()
    {
        var (businessId, owner) = await RegisterBusinessAsync();
        await TestPremium.UpgradeAsync(_factory, owner, businessId);

        var res = await owner.PutAsJsonAsync($"/businesses/{businessId}/plan", new SetPlanRequest("free"));
        res.EnsureSuccessStatusCode();

        Assert.Equal("free", await GetPlanAsync(owner, businessId));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        var sub = await db.Subscriptions.SingleAsync(s => s.BusinessId == businessId);
        Assert.Equal("cancelled", sub.Status);
        Assert.NotNull(sub.CancelledAt);
    }

    [Fact]
    public async Task UnknownSimulatedToken_Returns404()
    {
        using var raw = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        Assert.Equal(HttpStatusCode.NotFound, (await raw.GetAsync("/checkout/simulated/no-existe")).StatusCode);
    }

    // --- Webhook de Stripe -------------------------------------------------------

    private static string StripeSignature(string payload, long timestamp)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(WebhookSecret));
        var v1 = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}")));
        return $"t={timestamp},v1={v1}";
    }

    [Fact]
    public async Task StripeWebhook_SignedSessionCompleted_ActivatesPendingSubscription()
    {
        var (businessId, owner) = await RegisterBusinessAsync();
        // Suscripción 'pending' como la dejaría el checkout real de Stripe.
        var sessionId = $"cs_test_{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
            db.Subscriptions.Add(new Slotify.Domain.Entities.Subscription
            {
                Id = Guid.NewGuid(), BusinessId = businessId, Provider = "stripe",
                ExternalId = sessionId, Status = "pending", CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var payload = JsonSerializer.Serialize(new
        {
            type = "checkout.session.completed",
            data = new { @object = new { id = sessionId } },
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Stripe-Signature", StripeSignature(payload, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));

        var res = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("premium", await GetPlanAsync(owner, businessId));
    }

    [Fact]
    public async Task StripeWebhook_InvalidSignature_Returns400()
    {
        var payload = """{"type":"checkout.session.completed","data":{"object":{"id":"cs_x"}}}""";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Stripe-Signature", "t=123,v1=deadbeef");

        Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(request)).StatusCode);
    }

    /// <summary>Factory con el secreto del webhook configurado (checkout sigue simulado).</summary>
    public sealed class WebhookFactory : SlotifyApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("STRIPE_WEBHOOK_SECRET", WebhookSecret);
        }
    }
}
