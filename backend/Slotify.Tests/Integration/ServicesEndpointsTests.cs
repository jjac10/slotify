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

    private async Task<Guid> CreateServiceAsync(HttpClient owner, Guid businessId, string name = "Corte")
    {
        var svc = await (await owner.PostAsJsonAsync($"/businesses/{businessId}/services", Service(name)))
            .Content.ReadFromJsonAsync<ServiceResponse>();
        return svc!.Id;
    }

    [Fact]
    public async Task UpdateService_AsOwner_Returns200_AndReflectedInList()
    {
        var (businessId, token) = await RegisterOwnerAsync();
        var owner = Authorized(token);
        var serviceId = await CreateServiceAsync(owner, businessId, "Corte");

        var update = await owner.PutAsJsonAsync($"/businesses/{businessId}/services/{serviceId}",
            new UpdateServiceRequest("Corte premium", "Con lavado", 45, 20m, "#000000"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var list = await _client.GetFromJsonAsync<List<ServiceResponse>>($"/businesses/{businessId}/services");
        var svc = list!.Single(s => s.Id == serviceId);
        Assert.Equal("Corte premium", svc.Name);
        Assert.Equal(45, svc.DurationMinutes);
    }

    [Fact]
    public async Task DeleteService_AsOwner_Returns204_AndDisappearsFromList()
    {
        var (businessId, token) = await RegisterOwnerAsync();
        var owner = Authorized(token);
        var serviceId = await CreateServiceAsync(owner, businessId, "Corte");

        var delete = await owner.DeleteAsync($"/businesses/{businessId}/services/{serviceId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var list = await _client.GetFromJsonAsync<List<ServiceResponse>>($"/businesses/{businessId}/services");
        Assert.DoesNotContain(list!, s => s.Id == serviceId);
    }

    [Fact]
    public async Task UpdateService_NotOwner_Returns403()
    {
        var (businessId, token) = await RegisterOwnerAsync();
        var serviceId = await CreateServiceAsync(Authorized(token), businessId);
        var (_, otherToken) = await RegisterOwnerAsync();

        var res = await Authorized(otherToken).PutAsJsonAsync($"/businesses/{businessId}/services/{serviceId}",
            new UpdateServiceRequest("Hack", null, 30, null, null));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task DeleteService_Unknown_Returns404()
    {
        var (businessId, token) = await RegisterOwnerAsync();

        var res = await Authorized(token).DeleteAsync($"/businesses/{businessId}/services/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
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
