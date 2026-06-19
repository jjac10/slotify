using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// Confirmación de reservas end-to-end (POST /reservations/{id}/confirm). En negocios
/// con confirmación manual la reserva nace 'pending' y el owner/staff la confirma; en
/// 'auto' nace 'confirmed' y confirmarla de nuevo da 409. 401/403 según autorización.
/// </summary>
public class ReservationConfirmEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly SlotifyApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid businessId, Guid serviceId, Guid staffId, HttpClient owner)> SetupAsync(string confirmationMode)
    {
        var req = new RegisterOwnerRequest($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería");
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner", req)).Content.ReadFromJsonAsync<AuthResult>();
        var businessId = auth!.BusinessId!.Value;
        var owner = _factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        await owner.PutAsJsonAsync($"/businesses/{businessId}/confirmation-mode", new SetConfirmationModeRequest(confirmationMode));

        var service = await (await owner.PostAsJsonAsync($"/businesses/{businessId}/services",
            new CreateServiceRequest("Corte", null, 30, 15m, null))).Content.ReadFromJsonAsync<ServiceResponse>();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        var staffId = await db.Staff.Where(s => s.BusinessId == businessId).Select(s => s.Id).FirstAsync();
        return (businessId, service!.Id, staffId, owner);
    }

    private async Task<ReservationResponse> BookGuestAsync(Guid businessId, Guid serviceId, Guid staffId, DateTime start, string phone)
    {
        var booking = new CreateReservationRequest(businessId, serviceId, staffId, start, "Juan", phone, null);
        return (await (await _client.PostAsJsonAsync("/reservations", booking)).Content.ReadFromJsonAsync<ReservationResponse>())!;
    }

    [Fact]
    public async Task ManualBusiness_BookingIsPending_OwnerConfirms_Returns200_AndAudits()
    {
        var (businessId, serviceId, staffId, owner) = await SetupAsync("manual");
        var start = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        var booked = await BookGuestAsync(businessId, serviceId, staffId, start, "+34900001010");
        Assert.Equal("pending", booked.Status); // confirmación manual

        var res = await owner.PostAsync($"/reservations/{booked.Id}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ReservationResponse>();
        Assert.Equal("confirmed", body!.Status);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.ReservationId == booked.Id && a.Action == "confirmed" && a.ActorType == "owner"));
    }

    [Fact]
    public async Task AutoBusiness_BookingIsConfirmed_ConfirmAgainReturns409()
    {
        var (businessId, serviceId, staffId, owner) = await SetupAsync("auto");
        var start = new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);
        var booked = await BookGuestAsync(businessId, serviceId, staffId, start, "+34900001011");
        Assert.Equal("confirmed", booked.Status); // confirmación automática

        var res = await owner.PostAsync($"/reservations/{booked.Id}/confirm", null);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Confirm_WithoutToken_Returns401()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync("manual");
        var start = new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc);
        var booked = await BookGuestAsync(businessId, serviceId, staffId, start, "+34900001012");

        var res = await _client.PostAsync($"/reservations/{booked.Id}/confirm", null);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Confirm_ByOtherOwner_Returns403()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync("manual");
        var start = new DateTime(2026, 9, 4, 10, 0, 0, DateTimeKind.Utc);
        var booked = await BookGuestAsync(businessId, serviceId, staffId, start, "+34900001013");
        var (_, _, _, otherOwner) = await SetupAsync("manual");

        var res = await otherOwner.PostAsync($"/reservations/{booked.Id}/confirm", null);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
