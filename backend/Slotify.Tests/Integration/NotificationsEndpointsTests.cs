using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// Avisos end-to-end: configuración por negocio (canales + recordatorio) y registro de
/// notificaciones al crear/cancelar reservas (sender simulado → estado 'logged').
/// </summary>
public class NotificationsEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly SlotifyApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid businessId, Guid serviceId, Guid staffId, HttpClient owner)> SetupAsync()
    {
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner",
            new RegisterOwnerRequest($"o-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", $"Barberia {Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<AuthResult>();
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

    [Fact]
    public async Task SetNotificationSettings_AsOwner_Persists()
    {
        var (businessId, _, _, owner) = await SetupAsync();

        var res = await owner.PutAsJsonAsync($"/businesses/{businessId}/notification-settings",
            new SetNotificationSettingsRequest(NotifyByEmail: false, NotifyByWhatsapp: true, ReminderHoursBefore: 48));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<BusinessResponse>();
        Assert.False(body!.NotifyByEmail);
        Assert.True(body.NotifyByWhatsapp);
        Assert.Equal(48, body.ReminderHoursBefore);
    }

    [Fact]
    public async Task SetNotificationSettings_InvalidReminderHours_Returns400()
    {
        var (businessId, _, _, owner) = await SetupAsync();
        var res = await owner.PutAsJsonAsync($"/businesses/{businessId}/notification-settings",
            new SetNotificationSettingsRequest(true, false, 500));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task SetNotificationSettings_ByOtherOwner_Returns403()
    {
        var (businessId, _, _, _) = await SetupAsync();
        var (_, _, _, otherOwner) = await SetupAsync();
        var res = await otherOwner.PutAsJsonAsync($"/businesses/{businessId}/notification-settings",
            new SetNotificationSettingsRequest(true, false, 24));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task CreatingGuestBookingWithEmail_RecordsNotification()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync(); // por defecto NotifyByEmail = true

        var start = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
        var booking = new CreateReservationRequest(businessId, serviceId, staffId, start, "Juan", null, "juan@correo.test");
        var created = await (await _client.PostAsJsonAsync("/reservations", booking)).Content.ReadFromJsonAsync<ReservationResponse>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.ReservationId == created!.Id && n.EventType == "created");
        Assert.NotNull(notification);
        Assert.Equal("email", notification!.Channel);
        Assert.Equal("juan@correo.test", notification.Recipient);
        Assert.Equal("logged", notification.Status);
    }

    [Fact]
    public async Task GuestBookingWithoutEmail_AndEmailOnlyChannel_RecordsNoNotification()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync(); // email on, whatsapp off

        var start = new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc);
        var booking = new CreateReservationRequest(businessId, serviceId, staffId, start, "Juan", "+34900111222", null);
        var created = await (await _client.PostAsJsonAsync("/reservations", booking)).Content.ReadFromJsonAsync<ReservationResponse>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        Assert.False(await db.Notifications.AnyAsync(n => n.ReservationId == created!.Id));
    }
}
