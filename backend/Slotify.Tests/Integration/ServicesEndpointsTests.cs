using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Slotify.Domain.DTOs;

namespace Slotify.Tests.Integration;

/// <summary>
/// Endpoints de servicios y negocios contra la API real: alta (owner), límite
/// Freemium, autorización y listados.
/// </summary>
public class ServicesEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static RegisterOwnerRequest NewRegister() =>
        new($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería Pepe");

    /// <summary>Registra un owner y devuelve (businessId, accessToken).</summary>
    private async Task<(Guid businessId, string token)> RegisterOwnerAsync()
    {
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner", NewRegister()))
            .Content.ReadFromJsonAsync<AuthResult>();
        return (auth!.BusinessId!.Value, auth.AccessToken);
    }

    private HttpClient Authorized(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static CreateServiceRequest Service(string name = "Corte") =>
        new(name, "desc", 30, 15.00m, "#FF5733");

    [Fact]
    public async Task CreateService_AsOwner_Returns201_AndAppearsInList()
    {
        var (businessId, token) = await RegisterOwnerAsync();

        var create = await Authorized(token).PostAsJsonAsync($"/businesses/{businessId}/services", Service());
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var list = await _client.GetFromJsonAsync<List<ServiceResponse>>($"/businesses/{businessId}/services");
        Assert.Single(list!);
        Assert.Equal("Corte", list![0].Name);
    }

    [Fact]
    public async Task CreateService_WithoutToken_Returns401()
    {
        var (businessId, _) = await RegisterOwnerAsync();

        var response = await _client.PostAsJsonAsync($"/businesses/{businessId}/services", Service());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateService_NotOwner_Returns403()
    {
        var (businessId, _) = await RegisterOwnerAsync();   // negocio de A
        var (_, tokenB) = await RegisterOwnerAsync();        // otro owner B

        var response = await Authorized(tokenB).PostAsJsonAsync($"/businesses/{businessId}/services", Service());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateService_OverFreeLimit_Returns409()
    {
        var (businessId, token) = await RegisterOwnerAsync();
        var client = Authorized(token);

        // El plan Free permite 5 servicios.
        for (var i = 1; i <= 5; i++)
            (await client.PostAsJsonAsync($"/businesses/{businessId}/services", Service($"S{i}"))).EnsureSuccessStatusCode();

        var sixth = await client.PostAsJsonAsync($"/businesses/{businessId}/services", Service("S6"));

        Assert.Equal(HttpStatusCode.Conflict, sixth.StatusCode);
    }

    [Fact]
    public async Task ListServices_IsPublic_Returns200()
    {
        var (businessId, _) = await RegisterOwnerAsync();

        var response = await _client.GetAsync($"/businesses/{businessId}/services");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetBusinesses_AsOwner_ReturnsOwnBusiness()
    {
        var (businessId, token) = await RegisterOwnerAsync();

        var list = await Authorized(token).GetFromJsonAsync<List<BusinessResponse>>("/businesses");

        Assert.Contains(list!, b => b.Id == businessId);
    }
}
