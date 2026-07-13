using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// Tiempo real end-to-end (SignalR, /hubs/reservations): el cliente autenticado recibe
/// "reservationChanged" cuando el negocio confirma su reserva; el owner, unido al grupo
/// de su negocio (JoinBusiness), lo recibe cuando entra una reserva nueva. Sin token la
/// conexión se rechaza y JoinBusiness de un extraño falla. Transporte LongPolling (el
/// TestServer no habla WebSockets reales).
/// </summary>
public class RealtimeEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly SlotifyApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    private HubConnection Connect(string? accessToken)
        => new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/reservations", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (accessToken is not null)
                    options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .Build();

    private async Task<(Guid businessId, Guid serviceId, Guid staffId, HttpClient owner, string ownerToken)> SetupManualBusinessAsync()
    {
        var req = new RegisterOwnerRequest($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería");
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner", req)).Content.ReadFromJsonAsync<AuthResult>();
        var businessId = auth!.BusinessId!.Value;
        var owner = _factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        await owner.PutAsJsonAsync($"/businesses/{businessId}/confirmation-mode", new SetConfirmationModeRequest("manual"));
        var service = await (await owner.PostAsJsonAsync($"/businesses/{businessId}/services",
            new CreateServiceRequest("Corte", null, 30, 15m, null))).Content.ReadFromJsonAsync<ServiceResponse>();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        var staffId = await db.Staff.Where(s => s.BusinessId == businessId).Select(s => s.Id).FirstAsync();
        return (businessId, service!.Id, staffId, owner, auth.AccessToken);
    }

    [Fact]
    public async Task Customer_ReceivesReservationChanged_WhenOwnerConfirms()
    {
        var (businessId, serviceId, staffId, owner, _) = await SetupManualBusinessAsync();

        // Cliente registrado que reserva (queda pending: confirmación manual).
        var reg = await (await _client.PostAsJsonAsync("/auth/register",
            new RegisterCustomerRequest($"cli-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Ana")))
            .Content.ReadFromJsonAsync<AuthResult>();
        var customer = _factory.CreateClient();
        customer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reg!.AccessToken);
        var booked = await (await customer.PostAsJsonAsync("/reservations", new CreateReservationRequest(
            businessId, serviceId, staffId, new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc), null, null, null)))
            .Content.ReadFromJsonAsync<ReservationResponse>();
        Assert.Equal("pending", booked!.Status);

        // El cliente escucha su canal en tiempo real…
        await using var connection = Connect(reg.AccessToken);
        var received = new TaskCompletionSource<ReservationChangedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<ReservationChangedEvent>("reservationChanged", e => received.TrySetResult(e));
        await connection.StartAsync();

        // …y el negocio confirma.
        (await owner.PostAsync($"/reservations/{booked.Id}/confirm", null)).EnsureSuccessStatusCode();

        var evt = await received.Task.WaitAsync(EventTimeout);
        Assert.Equal(booked.Id, evt.ReservationId);
        Assert.Equal(businessId, evt.BusinessId);
        Assert.Equal("confirmed", evt.EventType);
    }

    [Fact]
    public async Task Owner_JoinedToBusinessGroup_ReceivesCreatedEvent_OnGuestBooking()
    {
        var (businessId, serviceId, staffId, _, ownerToken) = await SetupManualBusinessAsync();

        await using var connection = Connect(ownerToken);
        var received = new TaskCompletionSource<ReservationChangedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<ReservationChangedEvent>("reservationChanged", e => received.TrySetResult(e));
        await connection.StartAsync();
        await connection.InvokeAsync("JoinBusiness", businessId);

        // Un invitado (sin cuenta ni conexión) reserva → la Agenda del owner se entera.
        (await _client.PostAsJsonAsync("/reservations", new CreateReservationRequest(
            businessId, serviceId, staffId, new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc), "Juan", "+34910000041", null)))
            .EnsureSuccessStatusCode();

        var evt = await received.Task.WaitAsync(EventTimeout);
        Assert.Equal(businessId, evt.BusinessId);
        Assert.Equal("created", evt.EventType);
    }

    [Fact]
    public async Task JoinBusiness_ByStranger_IsRejected()
    {
        var (businessId, _, _, _, _) = await SetupManualBusinessAsync();
        var stranger = await (await _client.PostAsJsonAsync("/auth/register",
            new RegisterCustomerRequest($"str-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Otro")))
            .Content.ReadFromJsonAsync<AuthResult>();

        await using var connection = Connect(stranger!.AccessToken);
        await connection.StartAsync();

        await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("JoinBusiness", businessId));
    }

    [Fact]
    public async Task Connection_WithoutToken_IsRejected()
    {
        await using var connection = Connect(accessToken: null);

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }
}
