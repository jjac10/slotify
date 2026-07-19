using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Slotify.Tests.Integration;

/// <summary>
/// Sube un negocio a Premium por el flujo REAL de pago (checkout simulado completo):
/// abre el checkout y sigue su URL. Sustituye al antiguo PUT /plan premium, que ahora
/// está gateado tras el pago (409 payment_required).
/// </summary>
public static class TestPremium
{
    public static async Task UpgradeAsync(SlotifyApiFactory factory, HttpClient owner, Guid businessId)
    {
        var checkout = await owner.PostAsync($"/businesses/{businessId}/checkout", null);
        checkout.EnsureSuccessStatusCode();
        var url = (await checkout.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("url").GetString()!;
        var token = url.TrimEnd('/').Split('/')[^1];

        // Sin seguir la redirección al frontend: el 302 confirma el pago simulado.
        using var raw = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var complete = await raw.GetAsync($"/checkout/simulated/{token}");
        Assert.Equal(HttpStatusCode.Redirect, complete.StatusCode);
    }
}
