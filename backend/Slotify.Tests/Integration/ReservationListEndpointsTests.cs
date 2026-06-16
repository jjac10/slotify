using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// Listado de reservas end-to-end: agenda del negocio (owner/staff) con filtros y
/// "mis reservas" del usuario autenticado; 401/403.
/// </summary>
public class ReservationListEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly SlotifyApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid businessId, Guid serviceId, Guid staffId, HttpClient owner)> SetupAsync()
    {
        var req = new RegisterOwnerRequest($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería");
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner", req)).Content.ReadFromJsonAsync<AuthResult>();
        var businessId = auth!.BusinessId!.Value;
        var owner = _factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var service = await (await owner.PostAsJsonAsync($"/businesses/{businessId}/services",
            new CreateServiceRequest("Corte", null, 30, 15m, null))).Content.ReadFromJsonAsync<ServiceResponse>();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        var staffId = await db.Staff.Where(s => s.BusinessId == businessId).Select(s => s.Id).FirstAsync();
        return (businessId, service!.Id, staffId, owner);
    }

    private async Task<HttpClient> RegisterCustomerAsync()
    {
        var req = new RegisterCustomerRequest($"cust-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Ana");
        var auth = await (await _client.PostAsJsonAsync("/auth/register", req)).Content.ReadFromJsonAsync<AuthResult>();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    [Fact]
    public async Task ListForBusiness_AsOwner_Returns200_WithBooked_AndFiltersByDate()
    {
        var (businessId, serviceId, staffId, owner) = await SetupAsync();
        var day = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        await _client.PostAsJsonAsync("/reservations", new CreateReservationRequest(businessId, serviceId, staffId, day, "Juan", "+34900001001", null));
        await _client.PostAsJsonAsync("/reservations", new CreateReservationRequest(businessId, serviceId, staffId, day.AddDays(1), "Ana", "+34900001002", null));

        var all = await owner.GetFromJsonAsync<List<ReservationResponse>>($"/businesses/{businessId}/reservations");
        Assert.Equal(2, all!.Count);

        var onlyDay = await owner.GetFromJsonAsync<List<ReservationResponse>>(
            $"/businesses/{businessId}/reservations?date=2026-09-01");
        Assert.Single(onlyDay!);
    }

    [Fact]
    public async Task ListForBusiness_WithoutToken_Returns401()
    {
        var (businessId, _, _, _) = await SetupAsync();

        var res = await _client.GetAsync($"/businesses/{businessId}/reservations");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task ListForBusiness_ByOtherOwner_Returns403()
    {
        var (businessId, _, _, _) = await SetupAsync();
        var (_, _, _, otherOwner) = await SetupAsync();

        var res = await otherOwner.GetAsync($"/businesses/{businessId}/reservations");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task ListMine_ReturnsOnlyMyReservations()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync();
        var customer = await RegisterCustomerAsync();
        // El cliente reserva (autenticado → la reserva queda asociada a su user_id).
        var booking = new CreateReservationRequest(businessId, serviceId, staffId,
            new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc), null, null, null);
        (await customer.PostAsJsonAsync("/reservations", booking)).EnsureSuccessStatusCode();
        // Otra reserva de invitado (no debe aparecer en "mis reservas").
        await _client.PostAsJsonAsync("/reservations", new CreateReservationRequest(businessId, serviceId, staffId,
            new DateTime(2026, 9, 5, 14, 0, 0, DateTimeKind.Utc), "Juan", "+34900001003", null));

        var mine = await customer.GetFromJsonAsync<List<ReservationResponse>>("/reservations/mine");

        Assert.Single(mine!);
        Assert.NotNull(mine![0].UserId);
        Assert.Null(mine[0].GuestId);
    }

    [Fact]
    public async Task ListMine_WithoutToken_Returns401()
    {
        var res = await _client.GetAsync("/reservations/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
