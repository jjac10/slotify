using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// Panel del owner end-to-end: <c>GET /businesses/{id}/dashboard</c> con contadores,
/// ingresos del mes y próximas reservas. Owner-only: 401 sin token, 403 ajeno, 404 inexistente.
/// </summary>
public class DashboardEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
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
            new CreateServiceRequest("Corte", null, 30, 25m, null))).Content.ReadFromJsonAsync<ServiceResponse>();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        var staffId = await db.Staff.Where(s => s.BusinessId == businessId).Select(s => s.Id).FirstAsync();
        return (businessId, service!.Id, staffId, owner);
    }

    [Fact]
    public async Task Dashboard_AsOwner_Returns200_WithMetrics()
    {
        var (businessId, serviceId, staffId, owner) = await SetupAsync();
        // Una reserva futura (este mes, asumiendo ejecución antes de fin de mes la cuenta como upcoming).
        var future = DateTime.UtcNow.AddDays(1).Date.AddHours(10);
        await _client.PostAsJsonAsync("/reservations",
            new CreateReservationRequest(businessId, serviceId, staffId, future, "Juan", "+34900001001", null));

        var dash = await owner.GetFromJsonAsync<DashboardResponse>($"/businesses/{businessId}/dashboard");

        Assert.NotNull(dash);
        Assert.Equal(1, dash!.TotalReservations);
        Assert.Single(dash.UpcomingReservations);
        Assert.Equal(future, dash.UpcomingReservations[0].StartTime);
    }

    [Fact]
    public async Task Dashboard_WithoutToken_Returns401()
    {
        var (businessId, _, _, _) = await SetupAsync();

        var res = await _client.GetAsync($"/businesses/{businessId}/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Dashboard_ByOtherOwner_Returns403()
    {
        var (businessId, _, _, _) = await SetupAsync();
        var (_, _, _, otherOwner) = await SetupAsync();

        var res = await otherOwner.GetAsync($"/businesses/{businessId}/dashboard");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Dashboard_NonExistentBusiness_Returns404()
    {
        var (_, _, _, owner) = await SetupAsync();

        var res = await owner.GetAsync($"/businesses/{Guid.NewGuid()}/dashboard");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}
