using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>Reprogramación de reservas end-to-end: 200 + nuevo horario + auditoría; 401/403/409.</summary>
public class ReservationRescheduleEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
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
    public async Task Reschedule_AsOwner_Returns200_UpdatesTime_AndAudits()
    {
        var (businessId, serviceId, staffId, owner) = await SetupAsync();
        var start = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var id = await BookGuestAsync(businessId, serviceId, staffId, start, "+34900000010");

        var newStart = start.AddHours(3);
        var res = await owner.PatchAsJsonAsync($"/reservations/{id}", new RescheduleReservationRequest(newStart));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<ReservationResponse>();
        Assert.Equal(newStart, body!.StartTime);
        Assert.Equal(newStart.AddMinutes(30), body.EndTime); // conserva la duración del servicio

        // La reserva persiste con el nuevo horario.
        var get = await (await _client.GetAsync($"/reservations/{id}")).Content.ReadFromJsonAsync<ReservationResponse>();
        Assert.Equal(newStart, get!.StartTime);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.ReservationId == id && a.Action == "updated" && a.ActorType == "owner"));
    }

    [Fact]
    public async Task Reschedule_WithoutToken_Returns401()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync();
        var start = new DateTime(2026, 8, 2, 10, 0, 0, DateTimeKind.Utc);
        var id = await BookGuestAsync(businessId, serviceId, staffId, start, "+34900000011");

        var res = await _client.PatchAsJsonAsync($"/reservations/{id}", new RescheduleReservationRequest(start.AddHours(1)));

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Reschedule_ByOtherOwner_Returns403()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync();
        var start = new DateTime(2026, 8, 3, 10, 0, 0, DateTimeKind.Utc);
        var id = await BookGuestAsync(businessId, serviceId, staffId, start, "+34900000012");
        var (_, _, _, otherOwner) = await SetupAsync();

        var res = await otherOwner.PatchAsJsonAsync($"/reservations/{id}", new RescheduleReservationRequest(start.AddHours(1)));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Reschedule_OntoOccupiedSlot_Returns409()
    {
        var (businessId, serviceId, staffId, owner) = await SetupAsync();
        var slotA = new DateTime(2026, 8, 4, 10, 0, 0, DateTimeKind.Utc);
        var slotB = new DateTime(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc);
        await BookGuestAsync(businessId, serviceId, staffId, slotA, "+34900000013");          // ocupa 10:00–10:30
        var idB = await BookGuestAsync(businessId, serviceId, staffId, slotB, "+34900000014"); // 12:00–12:30

        // Mover B sobre el hueco ya ocupado por A.
        var res = await owner.PatchAsJsonAsync($"/reservations/{idB}", new RescheduleReservationRequest(slotA));

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }
}
