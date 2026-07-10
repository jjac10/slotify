using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// No asistencia end-to-end (POST /reservations/{id}/no-show): el owner marca una cita
/// pasada como 'no-show' (audita), una futura da 409 y la métrica aparece en el panel
/// (noShowsThisMonth / noShowRate). Las citas pasadas se siembran directamente en BD
/// (la API no deja reservar en el pasado).
/// </summary>
public class ReservationNoShowEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
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
            new CreateServiceRequest("Corte", null, 60, 15m, null))).Content.ReadFromJsonAsync<ServiceResponse>();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        var staffId = await db.Staff.Where(s => s.BusinessId == businessId).Select(s => s.Id).FirstAsync();
        return (businessId, service!.Id, staffId, owner);
    }

    /// <summary>Siembra en BD una reserva de invitado ya pasada (empezó hace N horas).</summary>
    private async Task<Guid> SeedPastReservationAsync(Guid businessId, Guid serviceId, Guid staffId, int hoursAgo)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        var guest = new Slotify.Domain.Entities.Guest
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Name = "Juan",
            PhoneEncrypted = "x",
            PhoneHash = Convert.ToHexString(Guid.NewGuid().ToByteArray()).PadLeft(64, '0')[..64],
            CreatedAt = DateTime.UtcNow,
        };
        db.Guests.Add(guest);
        var reservation = new Slotify.Domain.Entities.Reservation
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            ServiceId = serviceId,
            StaffId = staffId,
            GuestId = guest.Id,
            Status = "confirmed",
            StartTime = DateTime.UtcNow.AddHours(-hoursAgo),
            EndTime = DateTime.UtcNow.AddHours(-hoursAgo + 1),
        };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();
        return reservation.Id;
    }

    [Fact]
    public async Task Owner_MarksPastReservationNoShow_Returns200_Audits_AndFeedsDashboard()
    {
        var (businessId, serviceId, staffId, owner) = await SetupAsync();
        var id = await SeedPastReservationAsync(businessId, serviceId, staffId, hoursAgo: 3);

        var res = await owner.PostAsync($"/reservations/{id}/no-show", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("no-show", (await res.Content.ReadFromJsonAsync<ReservationResponse>())!.Status);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
            Assert.True(await db.AuditLogs.AnyAsync(a => a.ReservationId == id && a.Action == "no-show" && a.ActorType == "owner"));
        }

        // El panel refleja la métrica (1 pasada este mes, 1 no-show → 100 %).
        var dashboard = await (await owner.GetAsync($"/businesses/{businessId}/dashboard"))
            .Content.ReadFromJsonAsync<DashboardResponse>();
        Assert.Equal(1, dashboard!.NoShowsThisMonth);
        Assert.Equal(1.0, dashboard.NoShowRate);
        // Ocupación calculada (horario por defecto L–V sembrado al registrar): entre 0 y 1.
        Assert.NotNull(dashboard.OccupancyRate);
        Assert.InRange(dashboard.OccupancyRate!.Value, 0.0, 1.0);
    }

    [Fact]
    public async Task FutureReservation_CannotBeMarkedNoShow_Returns409()
    {
        var (businessId, serviceId, staffId, owner) = await SetupAsync();
        var booked = await (await _client.PostAsJsonAsync("/reservations", new CreateReservationRequest(
            businessId, serviceId, staffId, DateTime.UtcNow.AddDays(5).Date.AddHours(10), "Juan", "+34910000031", null)))
            .Content.ReadFromJsonAsync<ReservationResponse>();

        var res = await owner.PostAsync($"/reservations/{booked!.Id}/no-show", null);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("not_past", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Customer_CannotMarkNoShow_Returns403()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync();
        var id = await SeedPastReservationAsync(businessId, serviceId, staffId, hoursAgo: 2);

        var reg = await (await _client.PostAsJsonAsync("/auth/register",
            new RegisterCustomerRequest($"cli-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Ana")))
            .Content.ReadFromJsonAsync<AuthResult>();
        var customer = _factory.CreateClient();
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reg!.AccessToken);

        var res = await customer.PostAsync($"/reservations/{id}/no-show", null);

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}
