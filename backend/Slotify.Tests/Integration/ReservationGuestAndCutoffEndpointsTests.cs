using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Slotify.Domain.DTOs;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// Acciones de invitado (cancelar/reprogramar sin login) y ventana de antelación mínima
/// del negocio. La identidad del invitado se verifica con contacto + código OTP vigente
/// (pedido en POST /reservations/lookup/otp): sin código válido → 403 invalid_otp, aunque
/// el contacto sea el correcto. El owner no necesita OTP ni le aplica la ventana.
/// </summary>
public class ReservationGuestAndCutoffEndpointsTests : IClassFixture<ReservationGuestAndCutoffEndpointsTests.CapturingFactory>
{
    private readonly CapturingFactory _factory;
    private readonly HttpClient _client;

    public ReservationGuestAndCutoffEndpointsTests(CapturingFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

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

    /// <summary>Pide el OTP del contacto y lee el código del "SMS/email" capturado.</summary>
    private async Task<string> RequestOtpAsync(string contact)
    {
        (await _client.PostAsJsonAsync("/reservations/lookup/otp", new RequestGuestOtpRequest(contact)))
            .EnsureSuccessStatusCode();
        var code = _factory.Otps.LastCodeFor(contact);
        Assert.NotNull(code);
        return code!;
    }

    [Fact]
    public async Task Guest_CancelsOwnReservation_WithContactAndOtp_Returns204()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync();
        const string phone = "+34910000001";
        var id = await BookGuestAsync(businessId, serviceId, staffId, new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc), phone);

        var code = await RequestOtpAsync(phone);
        var res = await _client.PostAsJsonAsync($"/reservations/{id}/cancel",
            new CancelReservationRequest(Contact: phone, OtpCode: code));
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/reservations/{id}")).StatusCode);
    }

    [Fact]
    public async Task Guest_CancelWithoutOtp_Returns403_EvenWithCorrectContact()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync();
        const string phone = "+34910000006";
        var id = await BookGuestAsync(businessId, serviceId, staffId, new DateTime(2026, 10, 6, 10, 0, 0, DateTimeKind.Utc), phone);

        var res = await _client.PostAsJsonAsync($"/reservations/{id}/cancel",
            new CancelReservationRequest(Contact: phone));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Contains("invalid_otp", await res.Content.ReadAsStringAsync());
        // La reserva sigue viva.
        (await _client.GetAsync($"/reservations/{id}")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Guest_CancelWithWrongContact_Returns403()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync();
        var id = await BookGuestAsync(businessId, serviceId, staffId, new DateTime(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc), "+34910000002");

        // OTP válido... pero de OTRO contacto: la reserva no es suya → 403 (forbidden del dominio).
        const string other = "+34999999999";
        var code = await RequestOtpAsync(other);
        var res = await _client.PostAsJsonAsync($"/reservations/{id}/cancel",
            new CancelReservationRequest(Contact: other, OtpCode: code));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Guest_ReschedulesOwnReservation_WithContactAndOtp_Returns200()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync();
        const string phone = "+34910000003";
        var start = new DateTime(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc);
        var id = await BookGuestAsync(businessId, serviceId, staffId, start, phone);

        var code = await RequestOtpAsync(phone);
        var res = await _client.PatchAsJsonAsync($"/reservations/{id}",
            new RescheduleReservationRequest(start.AddHours(2), phone, code));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<ReservationResponse>();
        Assert.Equal(start.AddHours(2), body!.StartTime);
    }

    [Fact]
    public async Task Guest_RescheduleWithoutOtp_Returns403()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync();
        const string phone = "+34910000007";
        var start = new DateTime(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);
        var id = await BookGuestAsync(businessId, serviceId, staffId, start, phone);

        var res = await _client.PatchAsJsonAsync($"/reservations/{id}",
            new RescheduleReservationRequest(start.AddHours(2), phone));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Client_CancelWithinCutoffWindow_Returns409()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync(cutoffHours: 24);
        const string phone = "+34910000004";
        var id = await BookGuestAsync(businessId, serviceId, staffId, DateTime.UtcNow.AddHours(1), phone);

        var code = await RequestOtpAsync(phone);
        var res = await _client.PostAsJsonAsync($"/reservations/{id}/cancel",
            new CancelReservationRequest(Contact: phone, OtpCode: code));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Owner_CancelWithinCutoffWindow_Returns204()
    {
        var (businessId, serviceId, staffId, owner) = await SetupAsync(cutoffHours: 24);
        var id = await BookGuestAsync(businessId, serviceId, staffId, DateTime.UtcNow.AddHours(1), "+34910000005");

        // El owner no está sujeto a la ventana de antelación (ni al OTP: va con JWT).
        var res = await owner.PostAsJsonAsync($"/reservations/{id}/cancel", new CancelReservationRequest());
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    /// <summary>Factory que sustituye el sender de OTP por el capturador.</summary>
    public sealed class CapturingFactory : SlotifyApiFactory
    {
        public CapturingGuestOtpSender Otps { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGuestOtpSender>();
                services.AddSingleton<IGuestOtpSender>(Otps);
            });
        }
    }
}
