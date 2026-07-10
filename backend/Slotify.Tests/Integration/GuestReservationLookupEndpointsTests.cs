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
/// Un invitado (sin cuenta) ve sus reservas por teléfono/email con verificación de
/// identidad: pide un código OTP (POST /reservations/lookup/otp, 204 SIEMPRE:
/// anti-enumeración) que llega por su canal, y `POST /reservations/lookup` lo exige
/// (sin código o con código malo → 403 invalid_otp; jamás se listan reservas).
/// </summary>
public class GuestReservationLookupEndpointsTests : IClassFixture<GuestReservationLookupEndpointsTests.CapturingFactory>
{
    private readonly CapturingFactory _factory;
    private readonly HttpClient _client;

    public GuestReservationLookupEndpointsTests(CapturingFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> RequestOtpAsync(string contact)
    {
        (await _client.PostAsJsonAsync("/reservations/lookup/otp", new RequestGuestOtpRequest(contact)))
            .EnsureSuccessStatusCode();
        var code = _factory.Otps.LastCodeFor(NormalizeLikeBackend(contact));
        Assert.NotNull(code);
        return code!;
    }

    // El sender recibe el contacto ya normalizado (tel sin espacios / email en minúsculas).
    private static string NormalizeLikeBackend(string contact) =>
        contact.Contains('@') ? contact.Trim().ToLowerInvariant() : contact.Replace(" ", "");

    [Fact]
    public async Task Lookup_WithValidOtp_ReturnsTheirReservations()
    {
        var reg = new RegisterOwnerRequest($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería");
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner", reg)).Content.ReadFromJsonAsync<AuthResult>();
        var businessId = auth!.BusinessId!.Value;
        var owner = _factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var service = await (await owner.PostAsJsonAsync($"/businesses/{businessId}/services",
            new CreateServiceRequest("Corte", null, 30, 15m, null))).Content.ReadFromJsonAsync<ServiceResponse>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        var staffId = await db.Staff.Where(s => s.BusinessId == businessId).Select(s => s.Id).FirstAsync();

        var phone = $"+34600{Random.Shared.Next(100000, 999999)}";
        var day = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc);
        (await _client.PostAsJsonAsync("/reservations",
            new CreateReservationRequest(businessId, service!.Id, staffId, day, "Juan", phone, null))).EnsureSuccessStatusCode();

        // Con el código del "SMS/WhatsApp" → sus reservas, con los nombres enriquecidos.
        var code = await RequestOtpAsync(phone);
        var mine = await (await _client.PostAsJsonAsync("/reservations/lookup",
            new LookupGuestReservationsRequest(phone, code))).Content.ReadFromJsonAsync<List<ReservationResponse>>();
        Assert.Single(mine!);
        Assert.Equal("Barbería", mine![0].BusinessName);
        Assert.Equal("Corte", mine[0].ServiceName);

        // El código es reutilizable dentro de su ventana (lookup + gestionar después).
        var again = await _client.PostAsJsonAsync("/reservations/lookup",
            new LookupGuestReservationsRequest(phone, code));
        again.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Lookup_WithoutOtp_Returns403()
    {
        var res = await _client.PostAsJsonAsync("/reservations/lookup",
            new LookupGuestReservationsRequest("+34910000010"));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Contains("invalid_otp", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Lookup_WithWrongOtp_Returns403()
    {
        const string phone = "+34910000011";
        await RequestOtpAsync(phone);

        var res = await _client.PostAsJsonAsync("/reservations/lookup",
            new LookupGuestReservationsRequest(phone, "000000"));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task RequestOtp_UnknownContact_StillReturns204_AntiEnumeration()
    {
        // Contacto sin reservas: misma respuesta y también se envía código (nada que enumerar).
        var res = await _client.PostAsJsonAsync("/reservations/lookup/otp",
            new RequestGuestOtpRequest("nadie@desconocido.test"));

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.NotNull(_factory.Otps.LastCodeFor("nadie@desconocido.test"));
    }

    [Fact]
    public async Task RequestOtp_EmailContact_GoesThroughEmailChannel_PhoneThroughPhone()
    {
        (await _client.PostAsJsonAsync("/reservations/lookup/otp",
            new RequestGuestOtpRequest("Canal.Email@Test.local"))).EnsureSuccessStatusCode();
        (await _client.PostAsJsonAsync("/reservations/lookup/otp",
            new RequestGuestOtpRequest("+34 910 000 012"))).EnsureSuccessStatusCode();

        Assert.Contains(_factory.Otps.EmailCodes, c => c.Contact == "canal.email@test.local");
        Assert.Contains(_factory.Otps.PhoneCodes, c => c.Contact == "+34910000012");
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
