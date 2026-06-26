using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// Un invitado (sin cuenta) ve sus reservas por teléfono: `POST /reservations/lookup`
/// (contacto en el body) normaliza + blind index para encontrarlas. Contacto que no existe → vacío.
/// </summary>
public class GuestReservationLookupEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly SlotifyApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Lookup_ByGuestPhone_ReturnsTheirReservations()
    {
        var reg = new RegisterOwnerRequest($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería");
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner", reg)).Content.ReadFromJsonAsync<AuthResult>();
        var businessId = auth!.BusinessId!.Value;
        var owner = _factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var service = await (await owner.PostAsJsonAsync($"/businesses/{businessId}/services",
            new CreateServiceRequest("Corte", null, 30, 15m, null))).Content.ReadFromJsonAsync<ServiceResponse>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        var staffId = await db.Staff.Where(s => s.BusinessId == businessId).Select(s => s.Id).FirstAsync();

        var phone = $"+34600{Random.Shared.Next(100000, 999999)}";
        var day = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);
        (await _client.PostAsJsonAsync("/reservations",
            new CreateReservationRequest(businessId, service!.Id, staffId, day, "Juan", phone, null))).EnsureSuccessStatusCode();

        // Por el teléfono del invitado (en el body) → su reserva, con los nombres enriquecidos.
        var mine = await (await _client.PostAsJsonAsync("/reservations/lookup",
            new LookupGuestReservationsRequest(phone))).Content.ReadFromJsonAsync<List<ReservationResponse>>();
        Assert.Single(mine!);
        Assert.Equal("Barbería", mine![0].BusinessName);
        Assert.Equal("Corte", mine[0].ServiceName);

        // Contacto que no existe → vacío (no se filtran datos de otros).
        var none = await (await _client.PostAsJsonAsync("/reservations/lookup",
            new LookupGuestReservationsRequest("nadie@desconocido.test"))).Content.ReadFromJsonAsync<List<ReservationResponse>>();
        Assert.Empty(none!);
    }
}
