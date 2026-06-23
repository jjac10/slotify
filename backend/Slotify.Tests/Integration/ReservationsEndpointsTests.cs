using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>Endpoints de reservas contra la API real: alta de invitado, 409, 400 y consulta.</summary>
public class ReservationsEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly SlotifyApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();
    private static readonly DateTime At10 = new(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc);

    /// <summary>Registra owner, crea un servicio y devuelve (businessId, serviceId, staffId).</summary>
    private async Task<(Guid businessId, Guid serviceId, Guid staffId)> SetupBusinessAsync()
    {
        var owner = new RegisterOwnerRequest($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería");
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner", owner)).Content.ReadFromJsonAsync<AuthResult>();
        var businessId = auth!.BusinessId!.Value;

        var authed = _factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var service = await (await authed.PostAsJsonAsync($"/businesses/{businessId}/services",
            new CreateServiceRequest("Corte", null, 30, 15m, null))).Content.ReadFromJsonAsync<ServiceResponse>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        var staffId = await db.Staff.Where(s => s.BusinessId == businessId).Select(s => s.Id).FirstAsync();

        return (businessId, service!.Id, staffId);
    }

    private static CreateReservationRequest GuestReservation(
        (Guid businessId, Guid serviceId, Guid staffId) ctx, DateTime start, string phone) =>
        new(ctx.businessId, ctx.serviceId, ctx.staffId, start, "Juan", phone, null);

    [Fact]
    public async Task CreateReservation_AsGuest_Returns201_AndIsRetrievable()
    {
        var ctx = await SetupBusinessAsync();

        var create = await _client.PostAsJsonAsync("/reservations", GuestReservation(ctx, At10, "+34912345678"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var body = await create.Content.ReadFromJsonAsync<ReservationResponse>();
        Assert.NotNull(body!.GuestId);
        Assert.Equal(At10.AddMinutes(30), body.EndTime);

        var get = await _client.GetAsync($"/reservations/{body.Id}");
        get.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task CreateReservation_InvalidGuestContact_Returns400()
    {
        var ctx = await SetupBusinessAsync();
        // Ni teléfono ni email.
        var request = new CreateReservationRequest(ctx.businessId, ctx.serviceId, ctx.staffId, At10, "Juan", null, null);

        var response = await _client.PostAsJsonAsync("/reservations", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_Overlapping_Returns409()
    {
        var ctx = await SetupBusinessAsync();
        (await _client.PostAsJsonAsync("/reservations", GuestReservation(ctx, At10, "+34900000001"))).EnsureSuccessStatusCode();

        var overlap = await _client.PostAsJsonAsync("/reservations", GuestReservation(ctx, At10.AddMinutes(15), "+34900000002"));

        Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
    }

    [Fact]
    public async Task GetReservation_Unknown_Returns404()
    {
        var response = await _client.GetAsync($"/reservations/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_OwnerBooksThemselvesAsWorker_Returns400()
    {
        // Registramos un owner: su owner-staff queda enlazado a su propio user_id.
        var owner = new RegisterOwnerRequest($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería");
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner", owner)).Content.ReadFromJsonAsync<AuthResult>();
        var businessId = auth!.BusinessId!.Value;

        var authed = _factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var service = await (await authed.PostAsJsonAsync($"/businesses/{businessId}/services",
            new CreateServiceRequest("Corte", null, 30, 15m, null))).Content.ReadFromJsonAsync<ServiceResponse>();

        Guid ownerStaffId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
            ownerStaffId = await db.Staff
                .Where(s => s.BusinessId == businessId && s.UserId == auth.UserId)
                .Select(s => s.Id).FirstAsync();
        }

        // El owner (logueado) intenta reservar consigo mismo como trabajador asignado.
        var request = new CreateReservationRequest(businessId, service!.Id, ownerStaffId, At10, null, null, null);
        var response = await authed.PostAsJsonAsync("/reservations", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_AsAnonymousGuest_WithRegisteredEmail_Returns409()
    {
        // Un invitado SIN sesión pone el email del owner (cuenta registrada): no puede
        // reservar "como" esa cuenta — anti-suplantación. (Repro del bug reportado.)
        var ownerEmail = $"owner-{Guid.NewGuid():N}@test.local";
        var owner = new RegisterOwnerRequest(ownerEmail, "SecurePass123!", "Pepe", "Barbería");
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner", owner)).Content.ReadFromJsonAsync<AuthResult>();
        var businessId = auth!.BusinessId!.Value;
        var authed = _factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var service = await (await authed.PostAsJsonAsync($"/businesses/{businessId}/services",
            new CreateServiceRequest("Corte", null, 30, 15m, null))).Content.ReadFromJsonAsync<ServiceResponse>();
        Guid staffId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
            staffId = await db.Staff.Where(s => s.BusinessId == businessId).Select(s => s.Id).FirstAsync();
        }

        var request = new CreateReservationRequest(businessId, service!.Id, staffId, At10, "Suplantador", null, ownerEmail);
        var response = await _client.PostAsJsonAsync("/reservations", request); // sin token

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_OwnerBooksClientWithAccount_Returns201_AndLinks()
    {
        // El owner (recepción) SÍ puede apuntar la reserva a la cuenta de un cliente registrado.
        var ownerEmail = $"owner-{Guid.NewGuid():N}@test.local";
        var owner = new RegisterOwnerRequest(ownerEmail, "SecurePass123!", "Pepe", "Barbería");
        var ownerAuth = await (await _client.PostAsJsonAsync("/auth/register-owner", owner)).Content.ReadFromJsonAsync<AuthResult>();
        var businessId = ownerAuth!.BusinessId!.Value;
        var authed = _factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerAuth.AccessToken);
        var service = await (await authed.PostAsJsonAsync($"/businesses/{businessId}/services",
            new CreateServiceRequest("Corte", null, 30, 15m, null))).Content.ReadFromJsonAsync<ServiceResponse>();
        Guid staffId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
            staffId = await db.Staff.Where(s => s.BusinessId == businessId).Select(s => s.Id).FirstAsync();
        }

        // Cliente registrado cuyo email usará el owner al apuntar la reserva.
        var clientEmail = $"cli-{Guid.NewGuid():N}@test.local";
        var clientAuth = await (await _client.PostAsJsonAsync("/auth/register",
            new RegisterCustomerRequest(clientEmail, "SecurePass123!", "Ana"))).Content.ReadFromJsonAsync<AuthResult>();

        var request = new CreateReservationRequest(businessId, service!.Id, staffId, At10, "Ana", null, clientEmail);
        var response = await authed.PostAsJsonAsync("/reservations", request); // con token del owner
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ReservationResponse>();
        Assert.Equal(clientAuth!.UserId, body!.UserId); // vinculada a la cuenta del cliente
        Assert.Null(body.GuestId);
    }
}
