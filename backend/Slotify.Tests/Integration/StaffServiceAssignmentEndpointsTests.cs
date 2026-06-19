using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Slotify.Domain.DTOs;

namespace Slotify.Tests.Integration;

/// <summary>
/// Asignación de servicios a trabajadores contra la API real: el owner fija qué
/// servicios hace cada staff, y el listado público filtra staff por servicio
/// (un staff sin asignaciones puede realizar todos).
/// </summary>
public class StaffServiceAssignmentEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static RegisterOwnerRequest NewRegister() =>
        new($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería Pepe");

    private async Task<(Guid businessId, string token)> RegisterPremiumOwnerAsync()
    {
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner", NewRegister()))
            .Content.ReadFromJsonAsync<AuthResult>();
        var client = Authorized(auth!.AccessToken);
        // Premium para poder añadir empleados.
        (await client.PutAsJsonAsync($"/businesses/{auth.BusinessId}/plan", new SetPlanRequest("premium"))).EnsureSuccessStatusCode();
        return (auth.BusinessId!.Value, auth.AccessToken);
    }

    private HttpClient Authorized(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<Guid> CreateServiceAsync(HttpClient owner, Guid businessId, string name)
    {
        var svc = await (await owner.PostAsJsonAsync($"/businesses/{businessId}/services",
                new CreateServiceRequest(name, null, 30, 15m, "#FF0000")))
            .Content.ReadFromJsonAsync<ServiceResponse>();
        return svc!.Id;
    }

    private async Task<Guid> CreateEmployeeAsync(HttpClient owner, Guid businessId, string name)
    {
        var member = await (await owner.PostAsJsonAsync($"/businesses/{businessId}/staff",
                new CreateStaffRequest(name, null, null)))
            .Content.ReadFromJsonAsync<StaffResponse>();
        return member!.Id;
    }

    // ─── Set + Get ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetAndGetServices_AsOwner_Roundtrips()
    {
        var (businessId, token) = await RegisterPremiumOwnerAsync();
        var owner = Authorized(token);
        var s1 = await CreateServiceAsync(owner, businessId, "Corte");
        var s2 = await CreateServiceAsync(owner, businessId, "Tinte");
        var staffId = await CreateEmployeeAsync(owner, businessId, "Ana");

        var set = await owner.PutAsJsonAsync($"/businesses/{businessId}/staff/{staffId}/services",
            new SetStaffServicesRequest(new[] { s1 }));
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);

        var assigned = await owner.GetFromJsonAsync<List<Guid>>($"/businesses/{businessId}/staff/{staffId}/services");
        Assert.Single(assigned!);
        Assert.Contains(s1, assigned!);
        Assert.DoesNotContain(s2, assigned!);
    }

    // ─── Filtro público de staff por servicio ────────────────────────────────────

    [Fact]
    public async Task ListStaff_ByService_FiltersAssigned_AndIncludesUnassigned()
    {
        var (businessId, token) = await RegisterPremiumOwnerAsync();
        var owner = Authorized(token);
        var s1 = await CreateServiceAsync(owner, businessId, "Corte");
        var s2 = await CreateServiceAsync(owner, businessId, "Tinte");
        var anaId = await CreateEmployeeAsync(owner, businessId, "Ana");

        // Ana solo hace s1. El owner-staff no tiene asignaciones → hace todos.
        (await owner.PutAsJsonAsync($"/businesses/{businessId}/staff/{anaId}/services",
            new SetStaffServicesRequest(new[] { s1 }))).EnsureSuccessStatusCode();

        var forS1 = await _client.GetFromJsonAsync<List<StaffResponse>>($"/businesses/{businessId}/staff?serviceId={s1}");
        Assert.Equal(2, forS1!.Count); // owner + Ana

        var forS2 = await _client.GetFromJsonAsync<List<StaffResponse>>($"/businesses/{businessId}/staff?serviceId={s2}");
        Assert.Single(forS2!);                          // solo el owner (Ana no hace s2)
        Assert.DoesNotContain(forS2!, s => s.Id == anaId);
        Assert.Equal("owner", forS2![0].Role);
    }

    [Fact]
    public async Task ListStaff_WithoutServiceFilter_ReturnsAll()
    {
        var (businessId, token) = await RegisterPremiumOwnerAsync();
        var owner = Authorized(token);
        await CreateEmployeeAsync(owner, businessId, "Ana");

        var all = await _client.GetFromJsonAsync<List<StaffResponse>>($"/businesses/{businessId}/staff");

        Assert.Equal(2, all!.Count); // owner + Ana
    }

    // ─── Autorización / validación ───────────────────────────────────────────────

    [Fact]
    public async Task SetServices_ByOtherOwner_Returns403()
    {
        var (businessId, token) = await RegisterPremiumOwnerAsync();
        var owner = Authorized(token);
        var staffId = await CreateEmployeeAsync(owner, businessId, "Ana");
        var (_, otherToken) = await RegisterPremiumOwnerAsync();

        var res = await Authorized(otherToken).PutAsJsonAsync($"/businesses/{businessId}/staff/{staffId}/services",
            new SetStaffServicesRequest(Array.Empty<Guid>()));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task SetServices_WithServiceFromAnotherBusiness_Returns404()
    {
        var (businessId, token) = await RegisterPremiumOwnerAsync();
        var owner = Authorized(token);
        var staffId = await CreateEmployeeAsync(owner, businessId, "Ana");

        // Servicio de OTRO negocio.
        var (otherBusinessId, otherToken) = await RegisterPremiumOwnerAsync();
        var alienServiceId = await CreateServiceAsync(Authorized(otherToken), otherBusinessId, "Ajeno");

        var res = await owner.PutAsJsonAsync($"/businesses/{businessId}/staff/{staffId}/services",
            new SetStaffServicesRequest(new[] { alienServiceId }));

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task SetServices_WithoutToken_Returns401()
    {
        var (businessId, token) = await RegisterPremiumOwnerAsync();
        var staffId = await CreateEmployeeAsync(Authorized(token), businessId, "Ana");

        var res = await _client.PutAsJsonAsync($"/businesses/{businessId}/staff/{staffId}/services",
            new SetStaffServicesRequest(Array.Empty<Guid>()));

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
