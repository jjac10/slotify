using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// Endpoints de staff contra la API real: listado público (incluye al owner) +
/// gestión por el owner (alta/edición/baja), límite Freemium y autorización.
/// </summary>
public class StaffEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static RegisterOwnerRequest NewRegister() =>
        new($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería Pepe");

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

    /// <summary>Promueve el negocio a Premium (MaxStaff ilimitado) para poder añadir empleados.</summary>
    private async Task PromoteToPremiumAsync(Guid businessId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        var premium = await db.PricingTiers.AsNoTracking().SingleAsync(t => t.Code == "premium");
        var business = await db.Businesses.SingleAsync(b => b.Id == businessId);
        business.TierId = premium.Id;
        await db.SaveChangesAsync();
    }

    private static CreateStaffRequest Employee(string name = "Ana") =>
        new(name, $"{name.ToLowerInvariant()}@test.local", "+34600111222");

    // ─── Listado público ────────────────────────────────────────────────────────

    [Fact]
    public async Task ListStaff_IsPublic_ReturnsOwnerAsStaff()
    {
        var (businessId, _) = await RegisterOwnerAsync();

        var staff = await _client.GetFromJsonAsync<List<StaffResponse>>($"/businesses/{businessId}/staff");

        Assert.NotNull(staff);
        Assert.Single(staff!);
        Assert.Equal(businessId, staff![0].BusinessId);
        Assert.Equal("Pepe", staff[0].Name);
        Assert.Equal("owner", staff[0].Role);
    }

    // ─── Alta ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateStaff_AsOwnerOnPremium_Returns201_AndAppearsInList()
    {
        var (businessId, token) = await RegisterOwnerAsync();
        await PromoteToPremiumAsync(businessId);

        var create = await Authorized(token).PostAsJsonAsync($"/businesses/{businessId}/staff", Employee());
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var list = await _client.GetFromJsonAsync<List<StaffResponse>>($"/businesses/{businessId}/staff");
        Assert.Equal(2, list!.Count); // owner + Ana
        Assert.Contains(list, s => s.Name == "Ana" && s.Role == "employee");
    }

    [Fact]
    public async Task CreateStaff_WithoutToken_Returns401()
    {
        var (businessId, _) = await RegisterOwnerAsync();

        var response = await _client.PostAsJsonAsync($"/businesses/{businessId}/staff", Employee());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateStaff_NotOwner_Returns403()
    {
        var (businessId, _) = await RegisterOwnerAsync();   // negocio de A
        await PromoteToPremiumAsync(businessId);
        var (_, tokenB) = await RegisterOwnerAsync();        // otro owner B

        var response = await Authorized(tokenB).PostAsJsonAsync($"/businesses/{businessId}/staff", Employee());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateStaff_OnFreeTier_Returns409_LimitReached()
    {
        var (businessId, token) = await RegisterOwnerAsync(); // Free: el owner ya ocupa el único hueco

        var response = await Authorized(token).PostAsJsonAsync($"/businesses/{businessId}/staff", Employee());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ─── Edición ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStaff_AsOwner_RenamesEmployee()
    {
        var (businessId, token) = await RegisterOwnerAsync();
        await PromoteToPremiumAsync(businessId);
        var client = Authorized(token);
        var created = await (await client.PostAsJsonAsync($"/businesses/{businessId}/staff", Employee())).Content.ReadFromJsonAsync<StaffResponse>();

        var update = await client.PatchAsJsonAsync($"/businesses/{businessId}/staff/{created!.Id}", new UpdateStaffRequest("Ana María", "am@test.local", "+34600999888"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var list = await _client.GetFromJsonAsync<List<StaffResponse>>($"/businesses/{businessId}/staff");
        Assert.Contains(list!, s => s.Name == "Ana María");
    }

    // ─── Baja (soft delete) ─────────────────────────────────────────────────────

    [Fact]
    public async Task DeactivateStaff_AsOwner_RemovesFromActiveList()
    {
        var (businessId, token) = await RegisterOwnerAsync();
        await PromoteToPremiumAsync(businessId);
        var client = Authorized(token);
        var created = await (await client.PostAsJsonAsync($"/businesses/{businessId}/staff", Employee())).Content.ReadFromJsonAsync<StaffResponse>();

        var delete = await client.DeleteAsync($"/businesses/{businessId}/staff/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var list = await _client.GetFromJsonAsync<List<StaffResponse>>($"/businesses/{businessId}/staff");
        Assert.DoesNotContain(list!, s => s.Id == created.Id);
    }

    [Fact]
    public async Task DeactivateStaff_Owner_Returns409()
    {
        var (businessId, token) = await RegisterOwnerAsync();
        var staff = await _client.GetFromJsonAsync<List<StaffResponse>>($"/businesses/{businessId}/staff");
        var ownerStaffId = staff!.Single(s => s.Role == "owner").Id;

        var response = await Authorized(token).DeleteAsync($"/businesses/{businessId}/staff/{ownerStaffId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
