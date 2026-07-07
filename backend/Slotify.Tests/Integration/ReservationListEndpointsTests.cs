using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// Listado de reservas end-to-end: agenda del negocio (owner/staff) con filtros y
/// "mis reservas" del usuario autenticado, ambos paginados en BD devolviendo
/// { items, total, page, pageSize }; ?scope= en "mis reservas"; 400/401/403.
/// </summary>
public class ReservationListEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
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

    private async Task<(HttpClient client, Guid userId)> RegisterCustomerAsync()
    {
        var req = new RegisterCustomerRequest($"cust-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Ana");
        var auth = await (await _client.PostAsJsonAsync("/auth/register", req)).Content.ReadFromJsonAsync<AuthResult>();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (client, auth.UserId);
    }

    /// <summary>Inserta una reserva del usuario directamente en BD (permite inicios en el pasado).</summary>
    private async Task<Guid> SeedUserReservationAsync(
        (Guid businessId, Guid serviceId, Guid staffId, HttpClient owner) ctx, Guid userId, DateTime startUtc)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        var r = new Reservation
        {
            Id = Guid.NewGuid(), BusinessId = ctx.businessId, ServiceId = ctx.serviceId, StaffId = ctx.staffId,
            UserId = userId, Status = "confirmed", StartTime = startUtc, EndTime = startUtc.AddMinutes(30),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        db.Reservations.Add(r);
        await db.SaveChangesAsync();
        return r.Id;
    }

    // --- Agenda del negocio ----------------------------------------------------

    [Fact]
    public async Task ListForBusiness_AsOwner_Returns200_WithBooked_AndFiltersByDate()
    {
        var (businessId, serviceId, staffId, owner) = await SetupAsync();
        var day = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
        await _client.PostAsJsonAsync("/reservations", new CreateReservationRequest(businessId, serviceId, staffId, day, "Juan", "+34900001001", null));
        await _client.PostAsJsonAsync("/reservations", new CreateReservationRequest(businessId, serviceId, staffId, day.AddDays(1), "Ana", "+34900001002", null));

        var all = await owner.GetFromJsonAsync<PagedResponse<ReservationResponse>>($"/businesses/{businessId}/reservations");
        Assert.Equal(2, all!.Items.Count);
        Assert.Equal(2, all.Total);
        Assert.Equal(1, all.Page);
        Assert.Equal(20, all.PageSize); // default

        var onlyDay = await owner.GetFromJsonAsync<PagedResponse<ReservationResponse>>(
            $"/businesses/{businessId}/reservations?date=2026-09-01");
        Assert.Single(onlyDay!.Items);
        Assert.Equal(1, onlyDay.Total); // el total respeta el filtro
        // La agenda enriquece con el nombre del cliente (aquí, el invitado).
        Assert.Equal("Juan", onlyDay.Items[0].ClientName);
    }

    [Fact]
    public async Task ListForBusiness_Paginates_WithStableOrder_AndNoOverlap()
    {
        var (businessId, serviceId, staffId, owner) = await SetupAsync();
        var day = new DateTime(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 3; i++)
            (await _client.PostAsJsonAsync("/reservations", new CreateReservationRequest(
                businessId, serviceId, staffId, day.AddHours(i), "Juan", $"+3490000200{i}", null))).EnsureSuccessStatusCode();

        var page1 = await owner.GetFromJsonAsync<PagedResponse<ReservationResponse>>(
            $"/businesses/{businessId}/reservations?page=1&pageSize=2");
        var page2 = await owner.GetFromJsonAsync<PagedResponse<ReservationResponse>>(
            $"/businesses/{businessId}/reservations?page=2&pageSize=2");

        // Total del filtro en todas las páginas; sin solapes y orden por inicio.
        Assert.Equal(3, page1!.Total);
        Assert.Equal(3, page2!.Total);
        Assert.Equal(2, page1.Items.Count);
        Assert.Single(page2.Items);
        var ids = page1.Items.Concat(page2.Items).Select(r => r.Id).ToList();
        Assert.Equal(3, ids.Distinct().Count());
        var starts = page1.Items.Concat(page2.Items).Select(r => r.StartTime).ToList();
        Assert.Equal(starts.OrderBy(s => s), starts);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=51")]
    public async Task ListForBusiness_InvalidPagination_Returns400(string query)
    {
        var (businessId, _, _, owner) = await SetupAsync();

        var res = await owner.GetAsync($"/businesses/{businessId}/reservations?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("invalid_pagination", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ListForBusiness_WithoutToken_Returns401()
    {
        var (businessId, _, _, _) = await SetupAsync();

        var res = await _client.GetAsync($"/businesses/{businessId}/reservations");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task ListForBusiness_ByOtherOwner_Returns403()
    {
        var (businessId, _, _, _) = await SetupAsync();
        var (_, _, _, otherOwner) = await SetupAsync();

        var res = await otherOwner.GetAsync($"/businesses/{businessId}/reservations");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // --- Mis reservas ------------------------------------------------------------

    [Fact]
    public async Task ListMine_ReturnsOnlyMyReservations_Paged()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync();
        var (customer, _) = await RegisterCustomerAsync();
        // El cliente reserva (autenticado → la reserva queda asociada a su user_id).
        var booking = new CreateReservationRequest(businessId, serviceId, staffId,
            new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc), null, null, null);
        (await customer.PostAsJsonAsync("/reservations", booking)).EnsureSuccessStatusCode();
        // Otra reserva de invitado (no debe aparecer en "mis reservas").
        await _client.PostAsJsonAsync("/reservations", new CreateReservationRequest(businessId, serviceId, staffId,
            new DateTime(2026, 9, 5, 14, 0, 0, DateTimeKind.Utc), "Juan", "+34900001003", null));

        var mine = await customer.GetFromJsonAsync<PagedResponse<ReservationResponse>>("/reservations/mine");

        Assert.Single(mine!.Items);
        Assert.Equal(1, mine.Total);
        Assert.Equal(1, mine.Page);
        Assert.Equal(20, mine.PageSize); // default
        Assert.NotNull(mine.Items[0].UserId);
        Assert.Null(mine.Items[0].GuestId);
        // El listado enriquece con los nombres (negocio/servicio/trabajador).
        Assert.Equal("Barbería", mine.Items[0].BusinessName);
        Assert.Equal("Corte", mine.Items[0].ServiceName);
        Assert.Equal("Pepe", mine.Items[0].StaffName);
    }

    [Fact]
    public async Task ListMine_Paginates_WithTotalOfFilter()
    {
        var ctx = await SetupAsync();
        var (customer, userId) = await RegisterCustomerAsync();
        var baseStart = DateTime.UtcNow.AddDays(20);
        for (var i = 0; i < 3; i++)
            await SeedUserReservationAsync(ctx, userId, baseStart.AddHours(i));

        var page1 = await customer.GetFromJsonAsync<PagedResponse<ReservationResponse>>("/reservations/mine?page=1&pageSize=2");
        var page2 = await customer.GetFromJsonAsync<PagedResponse<ReservationResponse>>("/reservations/mine?page=2&pageSize=2");

        Assert.Equal(3, page1!.Total);
        Assert.Equal(3, page2!.Total);
        Assert.Equal(2, page1.Items.Count);
        Assert.Single(page2.Items);
        Assert.Equal(2, page2.Page);
        var ids = page1.Items.Concat(page2.Items).Select(r => r.Id).ToList();
        Assert.Equal(3, ids.Distinct().Count());
    }

    [Fact]
    public async Task ListMine_ScopeUpcoming_OnlyFuture_Ascending_AndPast_OnlyPast_Descending()
    {
        var ctx = await SetupAsync();
        var (customer, userId) = await RegisterCustomerAsync();
        var pastOld = await SeedUserReservationAsync(ctx, userId, DateTime.UtcNow.AddDays(-10));
        var pastRecent = await SeedUserReservationAsync(ctx, userId, DateTime.UtcNow.AddDays(-1));
        var futureNear = await SeedUserReservationAsync(ctx, userId, DateTime.UtcNow.AddDays(1));
        var futureFar = await SeedUserReservationAsync(ctx, userId, DateTime.UtcNow.AddDays(10));

        var upcoming = await customer.GetFromJsonAsync<PagedResponse<ReservationResponse>>("/reservations/mine?scope=upcoming");
        Assert.Equal(2, upcoming!.Total);
        Assert.Equal(new[] { futureNear, futureFar }, upcoming.Items.Select(r => r.Id).ToArray()); // ascendente

        var past = await customer.GetFromJsonAsync<PagedResponse<ReservationResponse>>("/reservations/mine?scope=past");
        Assert.Equal(2, past!.Total);
        Assert.Equal(new[] { pastRecent, pastOld }, past.Items.Select(r => r.Id).ToArray()); // reciente primero

        // Default (sin scope) = all: las 4, ascendente por inicio.
        var all = await customer.GetFromJsonAsync<PagedResponse<ReservationResponse>>("/reservations/mine");
        Assert.Equal(4, all!.Total);
        Assert.Equal(new[] { pastOld, pastRecent, futureNear, futureFar }, all.Items.Select(r => r.Id).ToArray());
    }

    [Fact]
    public async Task ListMine_InvalidScope_Returns400()
    {
        var (customer, _) = await RegisterCustomerAsync();

        var res = await customer.GetAsync("/reservations/mine?scope=yesterday");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("invalid_scope", await res.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=51")]
    public async Task ListMine_InvalidPagination_Returns400(string query)
    {
        var (customer, _) = await RegisterCustomerAsync();

        var res = await customer.GetAsync($"/reservations/mine?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("invalid_pagination", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ListMine_WithoutToken_Returns401()
    {
        var res = await _client.GetAsync("/reservations/mine");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
