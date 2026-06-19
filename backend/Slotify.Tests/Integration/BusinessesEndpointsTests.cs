using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Slotify.Domain.DTOs;

namespace Slotify.Tests.Integration;

/// <summary>Endpoint GET /businesses (lista los negocios del owner autenticado): 200 propios, 401 sin token.</summary>
public class BusinessesEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly SlotifyApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ListMine_WithoutToken_Returns401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/businesses")).StatusCode);
    }

    [Fact]
    public async Task ListMine_AsOwner_ReturnsOwnBusiness()
    {
        var req = new RegisterOwnerRequest($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería Pepe");
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner", req)).Content.ReadFromJsonAsync<AuthResult>();
        var owner = _factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var mine = await owner.GetFromJsonAsync<List<BusinessResponse>>("/businesses");

        Assert.Single(mine!);
        Assert.Equal(auth.BusinessId, mine![0].Id);
        Assert.Equal("Barbería Pepe", mine[0].Name);
    }

    [Fact]
    public async Task ListMine_DoesNotReturnOtherOwnersBusinesses()
    {
        // Owner A crea su negocio.
        var reqA = new RegisterOwnerRequest($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "A", "Negocio A");
        await _client.PostAsJsonAsync("/auth/register-owner", reqA);
        // Owner B solo debe ver el suyo.
        var reqB = new RegisterOwnerRequest($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "B", "Negocio B");
        var authB = await (await _client.PostAsJsonAsync("/auth/register-owner", reqB)).Content.ReadFromJsonAsync<AuthResult>();
        var ownerB = _factory.CreateClient();
        ownerB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authB!.AccessToken);

        var mine = await ownerB.GetFromJsonAsync<List<BusinessResponse>>("/businesses");

        Assert.Single(mine!);
        Assert.Equal("Negocio B", mine![0].Name);
    }

    // --- PUT /businesses/{id}/confirmation-mode -----------------------------

    private async Task<(Guid businessId, HttpClient owner)> RegisterOwnerAsync()
    {
        var req = new RegisterOwnerRequest($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería");
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner", req)).Content.ReadFromJsonAsync<AuthResult>();
        var owner = _factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (auth.BusinessId!.Value, owner);
    }

    [Fact]
    public async Task SetConfirmationMode_AsOwner_Returns200_AndPersists()
    {
        var (businessId, owner) = await RegisterOwnerAsync();

        var res = await owner.PutAsJsonAsync($"/businesses/{businessId}/confirmation-mode", new SetConfirmationModeRequest("manual"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<BusinessResponse>();
        Assert.Equal("manual", body!.ConfirmationMode);

        // Persiste: lo refleja el listado del owner.
        var mine = await owner.GetFromJsonAsync<List<BusinessResponse>>("/businesses");
        Assert.Equal("manual", mine!.Single(b => b.Id == businessId).ConfirmationMode);
    }

    [Fact]
    public async Task SetConfirmationMode_InvalidMode_Returns400()
    {
        var (businessId, owner) = await RegisterOwnerAsync();
        var res = await owner.PutAsJsonAsync($"/businesses/{businessId}/confirmation-mode", new SetConfirmationModeRequest("nope"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task SetConfirmationMode_ByOtherOwner_Returns403()
    {
        var (businessId, _) = await RegisterOwnerAsync();
        var (_, otherOwner) = await RegisterOwnerAsync();
        var res = await otherOwner.PutAsJsonAsync($"/businesses/{businessId}/confirmation-mode", new SetConfirmationModeRequest("manual"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task SetConfirmationMode_WithoutToken_Returns401()
    {
        var (businessId, _) = await RegisterOwnerAsync();
        var res = await _client.PutAsJsonAsync($"/businesses/{businessId}/confirmation-mode", new SetConfirmationModeRequest("manual"));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // --- PUT /businesses/{id}/cancellation-cutoff ---------------------------

    [Fact]
    public async Task SetCancellationCutoff_AsOwner_Returns200_AndPersists()
    {
        var (businessId, owner) = await RegisterOwnerAsync();

        var res = await owner.PutAsJsonAsync($"/businesses/{businessId}/cancellation-cutoff", new SetCancellationCutoffRequest(24));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<BusinessResponse>();
        Assert.Equal(24, body!.CancellationCutoffHours);

        var mine = await owner.GetFromJsonAsync<List<BusinessResponse>>("/businesses");
        Assert.Equal(24, mine!.Single(b => b.Id == businessId).CancellationCutoffHours);
    }

    [Fact]
    public async Task SetCancellationCutoff_OutOfRange_Returns400()
    {
        var (businessId, owner) = await RegisterOwnerAsync();
        var res = await owner.PutAsJsonAsync($"/businesses/{businessId}/cancellation-cutoff", new SetCancellationCutoffRequest(721));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task SetCancellationCutoff_ByOtherOwner_Returns403()
    {
        var (businessId, _) = await RegisterOwnerAsync();
        var (_, otherOwner) = await RegisterOwnerAsync();
        var res = await otherOwner.PutAsJsonAsync($"/businesses/{businessId}/cancellation-cutoff", new SetCancellationCutoffRequest(24));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
