using System.Net.Http.Headers;

namespace Slotify.Tests.Integration;

/// <summary>CORS habilitado para el frontend: una preflight desde el origen permitido recibe los headers CORS.</summary>
public class CorsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Preflight_FromAllowedOrigin_ReturnsCorsHeaders()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/reservations/mine");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _client.SendAsync(request);

        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task SimpleRequest_FromUnknownOrigin_HasNoCorsAllowHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/reservations/mine");
        request.Headers.Add("Origin", "https://evil.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
