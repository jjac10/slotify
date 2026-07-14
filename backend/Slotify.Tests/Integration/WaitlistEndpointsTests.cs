using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// Lista de espera end-to-end: apuntarse solo cuando el día está completo (si quedan
/// huecos → 409 slots_available), sin duplicarse (409 already_waiting), verla en
/// /me/waitlist y salir. Al cancelarse la reserva que llenaba el día, el primero de la
/// cola pasa a 'notified' y se registra su aviso (waitlist_slot_freed).
/// </summary>
public class WaitlistEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly SlotifyApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    private static DateOnly NextMonday()
    {
        var d = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7);
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(1);
        return d;
    }

    /// <summary>Negocio cuyo lunes solo tiene huecos entre 9:00 y <paramref name="closeMinutes"/>.</summary>
    private async Task<(Guid businessId, Guid serviceId, Guid staffId, HttpClient owner)> SetupAsync(int closeMinutes)
    {
        var req = new RegisterOwnerRequest($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería");
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner", req)).Content.ReadFromJsonAsync<AuthResult>();
        var businessId = auth!.BusinessId!.Value;
        var owner = _factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var service = await (await owner.PostAsJsonAsync($"/businesses/{businessId}/services",
            new CreateServiceRequest("Corte", null, 30, 15m, null))).Content.ReadFromJsonAsync<ServiceResponse>();
        (await owner.PutAsJsonAsync($"/businesses/{businessId}/hours", new SetBusinessHoursRequest(
            [new BusinessHourInput((int)DayOfWeek.Monday, false, new TimeOnly(9, 0), new TimeOnly(9, 0).AddMinutes(closeMinutes))])))
            .EnsureSuccessStatusCode();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        var staffId = await db.Staff.Where(s => s.BusinessId == businessId).Select(s => s.Id).FirstAsync();
        return (businessId, service!.Id, staffId, owner);
    }

    private async Task<HttpClient> RegisterCustomerAsync()
    {
        var auth = await (await _client.PostAsJsonAsync("/auth/register",
            new RegisterCustomerRequest($"cli-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Ana")))
            .Content.ReadFromJsonAsync<AuthResult>();
        var customer = _factory.CreateClient();
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return customer;
    }

    /// <summary>Convierte las 9:00 hora local Madrid del día al instante UTC (respeta DST).</summary>
    private static DateTime NineLocalUtc(DateOnly date)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");
        return TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(date.Year, date.Month, date.Day, 9, 0, 0, DateTimeKind.Unspecified), tz);
    }

    [Fact]
    public async Task FullDay_JoinListLeave_AndCancellationNotifiesFirstInQueue()
    {
        var monday = NextMonday();
        var (businessId, serviceId, staffId, owner) = await SetupAsync(closeMinutes: 30); // 1 solo hueco
        // Un invitado llena el único hueco del lunes.
        var booked = await (await _client.PostAsJsonAsync("/reservations", new CreateReservationRequest(
            businessId, serviceId, staffId, NineLocalUtc(monday), "Juan", "+34910000051", null)))
            .Content.ReadFromJsonAsync<ReservationResponse>();

        // El cliente se apunta a la cola (día completo) → 201 posición 1; repetir → 409.
        var customer = await RegisterCustomerAsync();
        var join = await customer.PostAsJsonAsync($"/businesses/{businessId}/waitlist",
            new JoinWaitlistRequest(serviceId, monday));
        Assert.Equal(HttpStatusCode.Created, join.StatusCode);
        var entry = await join.Content.ReadFromJsonAsync<WaitlistEntryResponse>();
        Assert.Equal(1, entry!.Position);
        Assert.Equal("waiting", entry.Status);

        var again = await customer.PostAsJsonAsync($"/businesses/{businessId}/waitlist",
            new JoinWaitlistRequest(serviceId, monday));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("already_waiting", await again.Content.ReadAsStringAsync());

        // La ve en /me/waitlist, con nombres.
        var mine = await (await customer.GetAsync("/me/waitlist")).Content.ReadFromJsonAsync<List<WaitlistEntryResponse>>();
        var listed = Assert.Single(mine!);
        Assert.Equal("Barbería", listed.BusinessName);
        Assert.Equal("Corte", listed.ServiceName);

        // El negocio cancela la reserva que llenaba el día → el primero pasa a notified
        // y su aviso queda registrado.
        (await owner.PostAsJsonAsync($"/reservations/{booked!.Id}/cancel", new CancelReservationRequest()))
            .EnsureSuccessStatusCode();

        mine = await (await customer.GetAsync("/me/waitlist")).Content.ReadFromJsonAsync<List<WaitlistEntryResponse>>();
        Assert.Equal("notified", Assert.Single(mine!).Status);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
            Assert.True(await db.Notifications.AnyAsync(n =>
                n.BusinessId == businessId && n.EventType == "waitlist_slot_freed"));
        }

        // Salir de la cola → 204 y desaparece.
        (await customer.DeleteAsync($"/waitlist/{listed.Id}")).EnsureSuccessStatusCode();
        mine = await (await customer.GetAsync("/me/waitlist")).Content.ReadFromJsonAsync<List<WaitlistEntryResponse>>();
        Assert.Empty(mine!);
    }

    [Fact]
    public async Task DayWithFreeSlots_JoinReturns409SlotsAvailable()
    {
        var monday = NextMonday();
        var (businessId, serviceId, staffId, _) = await SetupAsync(closeMinutes: 60); // 2 huecos
        // Solo se ocupa uno: queda hueco libre.
        (await _client.PostAsJsonAsync("/reservations", new CreateReservationRequest(
            businessId, serviceId, staffId, NineLocalUtc(monday), "Juan", "+34910000052", null)))
            .EnsureSuccessStatusCode();

        var customer = await RegisterCustomerAsync();
        var join = await customer.PostAsJsonAsync($"/businesses/{businessId}/waitlist",
            new JoinWaitlistRequest(serviceId, monday));

        Assert.Equal(HttpStatusCode.Conflict, join.StatusCode);
        Assert.Contains("slots_available", await join.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Anonymous_CannotJoin()
    {
        var (businessId, serviceId, _, _) = await SetupAsync(closeMinutes: 30);

        var res = await _client.PostAsJsonAsync($"/businesses/{businessId}/waitlist",
            new JoinWaitlistRequest(serviceId, NextMonday()));

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
