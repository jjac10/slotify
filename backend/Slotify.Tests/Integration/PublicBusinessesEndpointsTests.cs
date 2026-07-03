using System.Net.Http.Json;
using Slotify.Domain.DTOs;

namespace Slotify.Tests.Integration;

/// <summary>
/// Listado/búsqueda pública de negocios (`GET /public/businesses`): sin auth,
/// devuelve negocios activos, filtra por nombre con <c>?q=</c> y pagina en BD
/// con <c>?page=&amp;pageSize=</c> devolviendo { items, total, page, pageSize }.
/// </summary>
public class PublicBusinessesEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> SeedBusinessesAsync(int count)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < count; i++)
        {
            (await _client.PostAsJsonAsync("/auth/register-owner",
                new RegisterOwnerRequest($"o-{token}-{i}@test.local", "SecurePass123!", "Pepe", $"Negocio {token} {i:D2}")))
                .EnsureSuccessStatusCode();
        }
        return token;
    }

    [Fact]
    public async Task SearchPublic_NoAuth_ListsActive_AndFiltersByName()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var name = $"Barbería {token}";
        (await _client.PostAsJsonAsync("/auth/register-owner",
            new RegisterOwnerRequest($"o-{token}@test.local", "SecurePass123!", "Pepe", name)))
            .EnsureSuccessStatusCode();

        // Filtro por el token único → solo ese negocio, con total coherente.
        var filtered = await _client.GetFromJsonAsync<PagedResponse<BusinessResponse>>($"/public/businesses?q={token}");
        Assert.Single(filtered!.Items);
        Assert.Equal(1, filtered.Total);
        Assert.Equal(name, filtered.Items[0].Name);
    }

    [Fact]
    public async Task SearchPublic_Paginates_WithStableOrder_AndNoOverlap()
    {
        var token = await SeedBusinessesAsync(5);

        var page1 = await _client.GetFromJsonAsync<PagedResponse<BusinessResponse>>($"/public/businesses?q={token}&page=1&pageSize=2");
        var page2 = await _client.GetFromJsonAsync<PagedResponse<BusinessResponse>>($"/public/businesses?q={token}&page=2&pageSize=2");
        var page3 = await _client.GetFromJsonAsync<PagedResponse<BusinessResponse>>($"/public/businesses?q={token}&page=3&pageSize=2");

        // Total correcto con el filtro aplicado, en todas las páginas.
        Assert.Equal(5, page1!.Total);
        Assert.Equal(5, page2!.Total);
        Assert.Equal(5, page3!.Total);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(2, page2.Items.Count);
        Assert.Single(page3.Items);

        // Sin solapes entre páginas (orden estable por nombre + id).
        var ids = page1.Items.Concat(page2.Items).Concat(page3.Items).Select(b => b.Id).ToList();
        Assert.Equal(5, ids.Distinct().Count());

        // Orden estable: la concatenación respeta el orden por nombre.
        var names = page1.Items.Concat(page2.Items).Concat(page3.Items).Select(b => b.Name).ToList();
        Assert.Equal(names.OrderBy(n => n, StringComparer.Ordinal), names);
    }

    [Theory]
    [InlineData(0, 20)]    // 0 → default 20
    [InlineData(-7, 20)]   // negativo → default 20
    [InlineData(500, 50)]  // por encima del máximo → 50
    public async Task SearchPublic_ClampsPageSize(int requested, int expected)
    {
        var token = await SeedBusinessesAsync(1);

        var res = await _client.GetFromJsonAsync<PagedResponse<BusinessResponse>>(
            $"/public/businesses?q={token}&pageSize={requested}");

        Assert.Equal(expected, res!.PageSize);
        Assert.Equal(1, res.Total);
    }

    [Fact]
    public async Task SearchPublic_PageOutOfRange_ReturnsEmptyItems_WithCorrectTotal()
    {
        var token = await SeedBusinessesAsync(3);

        var res = await _client.GetFromJsonAsync<PagedResponse<BusinessResponse>>(
            $"/public/businesses?q={token}&page=99&pageSize=2");

        Assert.Empty(res!.Items);
        Assert.Equal(3, res.Total);
        Assert.Equal(99, res.Page);
    }

    [Fact]
    public async Task SearchPublic_PageZeroOrNegative_ClampsToFirstPage()
    {
        var token = await SeedBusinessesAsync(1);

        var res = await _client.GetFromJsonAsync<PagedResponse<BusinessResponse>>(
            $"/public/businesses?q={token}&page=0");

        Assert.Equal(1, res!.Page);
        Assert.Single(res.Items);
    }
}
