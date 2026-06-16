using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Slotify.API.Controllers;
using Slotify.Domain.DTOs;

namespace Slotify.Tests.Integration;

/// <summary>
/// Flujo de autenticación end-to-end contra la API real: register, login, refresh
/// y endpoint protegido /me.
/// </summary>
public class AuthEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static RegisterRequest NewRegister() =>
        new($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería Pepe");

    [Fact]
    public async Task Register_ReturnsCreatedWithTokens()
    {
        var response = await _client.PostAsJsonAsync("/auth/register", NewRegister());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResult>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.UserId);
        Assert.NotNull(body.BusinessId);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var request = NewRegister();
        (await _client.PostAsJsonAsync("/auth/register", request)).EnsureSuccessStatusCode();

        var duplicate = await _client.PostAsJsonAsync("/auth/register", request);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Login_AfterRegister_ReturnsTokens()
    {
        var request = NewRegister();
        (await _client.PostAsJsonAsync("/auth/register", request)).EnsureSuccessStatusCode();

        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(request.Email, request.Password));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResult>();
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var request = NewRegister();
        (await _client.PostAsJsonAsync("/auth/register", request)).EnsureSuccessStatusCode();

        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(request.Email, "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithToken_ReturnsCurrentUser()
    {
        var request = NewRegister();
        var registered = await (await _client.PostAsJsonAsync("/auth/register", request))
            .Content.ReadFromJsonAsync<AuthResult>();

        var message = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", registered!.AccessToken);
        var response = await _client.SendAsync(message);

        response.EnsureSuccessStatusCode();
        var me = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal(registered.UserId, me!.UserId);
        Assert.Equal(request.Email, me.Email);
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsRotatedTokens()
    {
        var registered = await (await _client.PostAsJsonAsync("/auth/register", NewRegister()))
            .Content.ReadFromJsonAsync<AuthResult>();

        var response = await _client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(registered!.RefreshToken));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResult>();
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.NotEqual(registered.RefreshToken, body.RefreshToken); // rotación
    }
}
