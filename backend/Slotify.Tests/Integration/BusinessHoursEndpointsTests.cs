using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Slotify.Domain.DTOs;

namespace Slotify.Tests.Integration;

/// <summary>Endpoints de horario y festivos contra la API real: owner los gestiona, lectura pública.</summary>
public class BusinessHoursEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly SlotifyApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid businessId, HttpClient owner)> RegisterOwnerAsync()
    {
        var req = new RegisterOwnerRequest($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería");
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner", req)).Content.ReadFromJsonAsync<AuthResult>();
        var owner = _factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (auth.BusinessId!.Value, owner);
    }

    private static SetBusinessHoursRequest Week() => new(new[]
    {
        new BusinessHourInput(1, false, new TimeOnly(9, 0), new TimeOnly(18, 0)),
        new BusinessHourInput(0, true, null, null),
    });

    [Fact]
    public async Task SetHours_AsOwner_Returns200_AndGetReturnsThem()
    {
        var (businessId, owner) = await RegisterOwnerAsync();

        var put = await owner.PutAsJsonAsync($"/businesses/{businessId}/hours", Week());
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var list = await _client.GetFromJsonAsync<List<BusinessHourResponse>>($"/businesses/{businessId}/hours");
        Assert.Equal(2, list!.Count);
        Assert.Contains(list, h => h.DayOfWeek == 1 && h.OpeningTime == new TimeOnly(9, 0));
    }

    [Fact]
    public async Task SetHours_WithoutToken_Returns401()
    {
        var (businessId, _) = await RegisterOwnerAsync();

        var response = await _client.PutAsJsonAsync($"/businesses/{businessId}/hours", Week());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SetHours_InvalidDay_Returns400()
    {
        var (businessId, owner) = await RegisterOwnerAsync();
        var bad = new SetBusinessHoursRequest(new[] { new BusinessHourInput(9, false, new TimeOnly(9, 0), new TimeOnly(18, 0)) });

        var response = await owner.PutAsJsonAsync($"/businesses/{businessId}/hours", bad);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetHours_NotOwner_Returns403()
    {
        var (businessId, _) = await RegisterOwnerAsync();
        var (_, otherOwner) = await RegisterOwnerAsync();

        var response = await otherOwner.PutAsJsonAsync($"/businesses/{businessId}/hours", Week());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddHoliday_AsOwner_Returns201_AndListedThenDeleted()
    {
        var (businessId, owner) = await RegisterOwnerAsync();

        var create = await owner.PostAsJsonAsync($"/businesses/{businessId}/holidays",
            new CreateHolidayRequest(new DateOnly(2026, 12, 25), "Navidad", true));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var holiday = await create.Content.ReadFromJsonAsync<BusinessHolidayResponse>();

        var list = await _client.GetFromJsonAsync<List<BusinessHolidayResponse>>($"/businesses/{businessId}/holidays");
        Assert.Single(list!);

        var delete = await owner.DeleteAsync($"/businesses/{businessId}/holidays/{holiday!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }
}
