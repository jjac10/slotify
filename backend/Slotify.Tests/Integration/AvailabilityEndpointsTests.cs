using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// Disponibilidad end-to-end: fijar horario → ver slots → reservar uno → el slot
/// desaparece. Contra la API real + Postgres.
/// </summary>
public class AvailabilityEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly SlotifyApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();
    // Próximo lunes con al menos una semana de margen → siempre futuro (el endpoint
    // descarta horas ya pasadas usando la hora actual real).
    private static readonly DateOnly Date = NextMonday();

    private static DateOnly NextMonday()
    {
        var d = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7);
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(1);
        return d;
    }

    private async Task<(Guid businessId, Guid serviceId, Guid staffId, HttpClient owner)> SetupAsync(int duration = 30)
    {
        var req = new RegisterOwnerRequest($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería");
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner", req)).Content.ReadFromJsonAsync<AuthResult>();
        var businessId = auth!.BusinessId!.Value;

        var owner = _factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var service = await (await owner.PostAsJsonAsync($"/businesses/{businessId}/services",
            new CreateServiceRequest("Corte", null, duration, 15m, null))).Content.ReadFromJsonAsync<ServiceResponse>();

        // Horario: lunes 9-12 abierto.
        await owner.PutAsJsonAsync($"/businesses/{businessId}/hours",
            new SetBusinessHoursRequest(new[] { new BusinessHourInput((int)Date.DayOfWeek, false, new TimeOnly(9, 0), new TimeOnly(12, 0)) }));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        var staffId = await db.Staff.Where(s => s.BusinessId == businessId).Select(s => s.Id).FirstAsync();

        return (businessId, service!.Id, staffId, owner);
    }

    [Fact]
    public async Task Availability_OpenDay_ReturnsSlots()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync(duration: 30);

        var slots = await _client.GetFromJsonAsync<List<AvailableSlot>>(
            $"/businesses/{businessId}/availability?serviceId={serviceId}&staffId={staffId}&date={Date:yyyy-MM-dd}");

        Assert.Equal(6, slots!.Count); // 9-12, 30 min → 6 slots
        // El horario (9:00) es hora local Europe/Madrid; se compara convertido a UTC
        // (respeta DST según la fecha concreta).
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");
        var expectedFirst = TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(Date.Year, Date.Month, Date.Day, 9, 0, 0, DateTimeKind.Unspecified), tz);
        Assert.Equal(expectedFirst, slots[0].Start);
    }

    [Fact]
    public async Task Availability_AfterBooking_ExcludesTakenSlot()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync(duration: 30);
        var first = $"/businesses/{businessId}/availability?serviceId={serviceId}&staffId={staffId}&date={Date:yyyy-MM-dd}";
        var before = await _client.GetFromJsonAsync<List<AvailableSlot>>(first);

        // Reservar el primer slot (09:00).
        var booking = new CreateReservationRequest(businessId, serviceId, staffId,
            before![0].Start, "Juan", "+34912345678", null);
        (await _client.PostAsJsonAsync("/reservations", booking)).EnsureSuccessStatusCode();

        var after = await _client.GetFromJsonAsync<List<AvailableSlot>>(first);

        Assert.Equal(before.Count - 1, after!.Count);
        Assert.DoesNotContain(after, s => s.Start == before[0].Start);
    }

    [Fact]
    public async Task Availability_ClosedDay_ReturnsEmpty()
    {
        var (businessId, serviceId, staffId, _) = await SetupAsync();
        var sunday = Date.AddDays(-1); // domingo (sin horario configurado)

        var slots = await _client.GetFromJsonAsync<List<AvailableSlot>>(
            $"/businesses/{businessId}/availability?serviceId={serviceId}&staffId={staffId}&date={sunday:yyyy-MM-dd}");

        Assert.Empty(slots!);
    }
}
