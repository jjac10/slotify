using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>Cancelación de reservas end-to-end: 204 + hard-delete + auditoría; 401/403.</summary>
public class ReservationCancelEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
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

    private async Task<Guid> BookGuestAsync(Guid businessId, Guid serviceId, Guid staffId, DateTime start, string phone)
    {
        var booking = new CreateReservationRequest(businessId, serviceId, staffId, start, "Juan", phone, null);
        var res = await (await _client.PostAsJsonAsync("/reservations", booking)).Content.ReadFromJsonAsync<ReservationResponse>();
        return res!.Id;
    }

    [Fact]
    public async Task Cancel_AsOwner_Returns204_HardDeletes_AndAudits()
    {
        var (businessId, serviceId, staffId, owner) = await SetupAsync();
        var id = await BookGuestAsync(businessId, serviceId, staffId, new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc), "+34900000001");

        var cancel = await owner.PostAsJsonAsync($"/reservations/{id}/cancel", new CancelReservationRequest("no puedo"));
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        // Hard-delete: ya no existe.
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/reservations/{id}")).StatusCode);

        // La auditoría persiste (con la reserva ya borrada → reservation_id null).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "cancelled" && a.ActorType == "owner"));
    }

    [Fact]
    public async Task Cancel_AsGuestWithoutContact_Returns403()
    {
        // Sin JWT y sin contacto se trata como invitado no verificado → 403 (ya no 401).
        var (businessId, serviceId, staffId, _) = await SetupAsync();
        var id = await BookGuestAsync(businessId, serviceId, staffId, new DateTime(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc), "+34900000002");

        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync($"/reservations/{id}/cancel", new CancelReservationRequest())).StatusCode);
    }

    [Fact]
    public async Task Cancel_ByOtherOwner_Returns403()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync();
        var id = await BookGuestAsync(businessId, serviceId, staffId, new DateTime(2026, 7, 3, 10, 0, 0, DateTimeKind.Utc), "+34900000003");
        var (_, _, _, otherOwner) = await SetupAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await otherOwner.PostAsJsonAsync($"/reservations/{id}/cancel", new CancelReservationRequest())).StatusCode);
    }
}
