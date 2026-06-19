using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// Acciones de invitado (cancelar/reprogramar sin login, verificado por contacto) y
/// ventana de antelación mínima del negocio: el cliente no puede dentro de la ventana,
/// el owner sí.
/// </summary>
public class ReservationGuestAndCutoffEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly SlotifyApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid businessId, Guid serviceId, Guid staffId, HttpClient owner)> SetupAsync(int cutoffHours = 0)
    {
        var req = new RegisterOwnerRequest($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería");
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner", req)).Content.ReadFromJsonAsync<AuthResult>();
        var businessId = auth!.BusinessId!.Value;
        var owner = _factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        if (cutoffHours > 0)
            await owner.PutAsJsonAsync($"/businesses/{businessId}/cancellation-cutoff", new SetCancellationCutoffRequest(cutoffHours));
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
        var res = await _client.PostAsJsonAsync("/reservations", booking);
        if (!res.IsSuccessStatusCode) throw new Exception($"book failed: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<ReservationResponse>())!.Id;
    }

    [Fact]
    public async Task Guest_CancelsOwnReservation_WithContact_Returns204()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync();
        const string phone = "+34910000001";
        var id = await BookGuestAsync(businessId, serviceId, staffId, new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc), phone);

        var res = await _client.DeleteAsync($"/reservations/{id}?contact={Uri.EscapeDataString(phone)}");
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/reservations/{id}")).StatusCode);
    }

    [Fact]
    public async Task Guest_CancelWithWrongContact_Returns403()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync();
        var id = await BookGuestAsync(businessId, serviceId, staffId, new DateTime(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc), "+34910000002");

        var res = await _client.DeleteAsync($"/reservations/{id}?contact={Uri.EscapeDataString("+34999999999")}");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Guest_ReschedulesOwnReservation_WithContact_Returns200()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync();
        const string phone = "+34910000003";
        var start = new DateTime(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc);
        var id = await BookGuestAsync(businessId, serviceId, staffId, start, phone);

        var res = await _client.PatchAsJsonAsync($"/reservations/{id}", new RescheduleReservationRequest(start.AddHours(2), phone));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ReservationResponse>();
        Assert.Equal(start.AddHours(2), body!.StartTime);
    }

    [Fact]
    public async Task Client_CancelWithinCutoffWindow_Returns409()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync(cutoffHours: 24);
        const string phone = "+34910000004";
        var id = await BookGuestAsync(businessId, serviceId, staffId, DateTime.UtcNow.AddHours(1), phone);

        var res = await _client.DeleteAsync($"/reservations/{id}?contact={Uri.EscapeDataString(phone)}");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Owner_CancelWithinCutoffWindow_Returns204()
    {
        var (businessId, serviceId, staffId, owner) = await SetupAsync(cutoffHours: 24);
        var id = await BookGuestAsync(businessId, serviceId, staffId, DateTime.UtcNow.AddHours(1), "+34910000005");

        // El owner no está sujeto a la ventana de antelación.
        var res = await owner.DeleteAsync($"/reservations/{id}");
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }
}
