using System.Net.Http.Json;
using Slotify.Domain.DTOs;

namespace Slotify.Tests.Integration;

/// <summary>
/// GET /businesses/{id}/staff contra la API real: listado público que incluye al
/// owner (creado como staff role='owner' al registrar el negocio).
/// </summary>
public class StaffEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static RegisterOwnerRequest NewRegister() =>
        new($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería Pepe");

    private async Task<Guid> RegisterOwnerBusinessAsync()
    {
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner", NewRegister()))
            .Content.ReadFromJsonAsync<AuthResult>();
        return auth!.BusinessId!.Value;
    }

    [Fact]
    public async Task ListStaff_IsPublic_ReturnsOwnerAsStaff()
    {
        var businessId = await RegisterOwnerBusinessAsync();

        var staff = await _client.GetFromJsonAsync<List<StaffResponse>>($"/businesses/{businessId}/staff");

        Assert.NotNull(staff);
        Assert.Single(staff!);
        Assert.Equal(businessId, staff![0].BusinessId);
        Assert.Equal("Pepe", staff[0].Name);
        Assert.Equal("owner", staff[0].Role);
    }

    [Fact]
    public async Task ListStaff_Public_Returns200()
    {
        var businessId = await RegisterOwnerBusinessAsync();

        var response = await _client.GetAsync($"/businesses/{businessId}/staff");

        response.EnsureSuccessStatusCode();
    }
}
