using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// Modo de reservas 'solo calendario' end-to-end: al activarlo el negocio desaparece
/// de Explorar (GET /public/businesses) y no acepta reservas online (un invitado recibe
/// 409), pero el owner sí puede apuntar reservas desde su agenda.
/// </summary>
public class BookingModeEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly SlotifyApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid businessId, string token, Guid serviceId, Guid staffId, HttpClient owner)> SetupAsync()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner",
            new RegisterOwnerRequest($"o-{token}@test.local", "SecurePass123!", "Pepe", $"Barberia {token}")))
            .Content.ReadFromJsonAsync<AuthResult>();
        var businessId = auth!.BusinessId!.Value;
        var owner = _factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var service = await (await owner.PostAsJsonAsync($"/businesses/{businessId}/services",
            new CreateServiceRequest("Corte", null, 30, 15m, null))).Content.ReadFromJsonAsync<ServiceResponse>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        var staffId = await db.Staff.Where(s => s.BusinessId == businessId).Select(s => s.Id).FirstAsync();
        return (businessId, token, service!.Id, staffId, owner);
    }

    [Fact]
    public async Task CalendarOnly_StillListedInExplore_ButBlocksGuestBooking_AndOwnerCanBook()
    {
        var (businessId, token, serviceId, staffId, owner) = await SetupAsync();

        // En 'online' (por defecto) aparece en Explorar.
        var before = await _client.GetFromJsonAsync<List<BusinessResponse>>($"/public/businesses?q={token}");
        Assert.Single(before!);

        // El owner lo pone en 'solo calendario'.
        var res = await owner.PutAsJsonAsync($"/businesses/{businessId}/booking-mode", new SetBookingModeRequest("calendar_only"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<BusinessResponse>();
        Assert.Equal("calendar_only", body!.BookingMode);

        // Sigue apareciendo en Explorar (la UI lo marca como "cita en persona") con su modo.
        var after = await _client.GetFromJsonAsync<List<BusinessResponse>>($"/public/businesses?q={token}");
        Assert.Single(after!);
        Assert.Equal("calendar_only", after![0].BookingMode);

        // Un invitado no puede reservar online → 409.
        var start = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
        var guestBooking = new CreateReservationRequest(businessId, serviceId, staffId, start, "Juan", "+34900100100", null);
        var guestRes = await _client.PostAsJsonAsync("/reservations", guestBooking);
        Assert.Equal(HttpStatusCode.Conflict, guestRes.StatusCode);

        // El owner sí puede apuntar la reserva desde su agenda.
        var ownerBooking = new CreateReservationRequest(businessId, serviceId, staffId, start, "Juan", "+34900100100", null);
        var ownerRes = await owner.PostAsJsonAsync("/reservations", ownerBooking);
        Assert.Equal(HttpStatusCode.Created, ownerRes.StatusCode);
    }

    [Fact]
    public async Task SetBookingMode_InvalidMode_Returns400()
    {
        var (businessId, _, _, _, owner) = await SetupAsync();
        var res = await owner.PutAsJsonAsync($"/businesses/{businessId}/booking-mode", new SetBookingModeRequest("nope"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task SetBookingMode_ByOtherOwner_Returns403()
    {
        var (businessId, _, _, _, _) = await SetupAsync();
        var (_, _, _, _, otherOwner) = await SetupAsync();
        var res = await otherOwner.PutAsJsonAsync($"/businesses/{businessId}/booking-mode", new SetBookingModeRequest("calendar_only"));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
