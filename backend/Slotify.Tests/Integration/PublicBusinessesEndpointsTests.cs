using System.Net.Http.Json;
using Slotify.Domain.DTOs;

namespace Slotify.Tests.Integration;

/// <summary>
/// Listado/búsqueda pública de negocios (`GET /public/businesses`): sin auth,
/// devuelve negocios activos y filtra por nombre con <c>?q=</c>.
/// </summary>
public class PublicBusinessesEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task SearchPublic_NoAuth_ListsActive_AndFiltersByName()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var name = $"Barbería {token}";
        (await _client.PostAsJsonAsync("/auth/register-owner",
            new RegisterOwnerRequest($"o-{token}@test.local", "SecurePass123!", "Pepe", name)))
            .EnsureSuccessStatusCode();

        // Sin filtro: el negocio recién creado aparece (sin necesidad de token).
        var all = await _client.GetFromJsonAsync<List<BusinessResponse>>("/public/businesses");
        Assert.Contains(all!, b => b.Name == name);

        // Filtro por el token único → solo ese negocio.
        var filtered = await _client.GetFromJsonAsync<List<BusinessResponse>>($"/public/businesses?q={token}");
        Assert.Single(filtered!);
        Assert.Equal(name, filtered![0].Name);
    }
}
