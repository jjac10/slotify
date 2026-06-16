using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// Sync invitado→usuario end-to-end: un invitado reserva con teléfono y, al
/// registrarse luego como cliente con ese mismo teléfono, su reserva queda vinculada.
/// </summary>
public class GuestSyncEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly SlotifyApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RegisterCustomer_LinksPreviousGuestReservation_BySamePhone()
    {
        const string phone = "+34912345678";

        // 1) Owner + servicio + staff.
        var owner = new RegisterOwnerRequest($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería");
        var ownerAuth = await (await _client.PostAsJsonAsync("/auth/register-owner", owner)).Content.ReadFromJsonAsync<AuthResult>();
        var businessId = ownerAuth!.BusinessId!.Value;

        var ownerClient = _factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerAuth.AccessToken);
        var service = await (await ownerClient.PostAsJsonAsync($"/businesses/{businessId}/services",
            new CreateServiceRequest("Corte", null, 30, 15m, null))).Content.ReadFromJsonAsync<ServiceResponse>();

        Guid staffId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
            staffId = await db.Staff.Where(s => s.BusinessId == businessId).Select(s => s.Id).FirstAsync();
        }

        // 2) Invitado reserva con teléfono.
        var booking = new CreateReservationRequest(businessId, service!.Id, staffId,
            new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc), "Juan", phone, null);
        (await _client.PostAsJsonAsync("/reservations", booking)).EnsureSuccessStatusCode();

        // 3) El invitado se registra como cliente con el MISMO teléfono.
        var customer = await (await _client.PostAsJsonAsync("/auth/register",
            new RegisterCustomerRequest($"juan-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Juan", phone)))
            .Content.ReadFromJsonAsync<AuthResult>();

        // 4) El guest de esa reserva queda vinculado al nuevo usuario.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
            var guest = await db.Guests.AsNoTracking().SingleAsync(g => g.BusinessId == businessId);
            Assert.Equal(customer!.UserId, guest.UserId);
        }
    }
}
