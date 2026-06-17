using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Slotify.API.Controllers;
using Slotify.Domain.DTOs;

namespace Slotify.Tests.Integration;

/// <summary>
/// Flujo de autenticación end-to-end contra la API real: registro (customer y
/// owner), login, refresh y endpoint protegido /me.
/// </summary>
public class AuthEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static RegisterOwnerRequest NewOwnerRegister() =>
        new($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería Pepe");

    private static RegisterCustomerRequest NewCustomerRegister() =>
        new($"cust-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Ana");

    [Fact]
    public async Task RegisterCustomer_ReturnsCreated_WithoutBusiness()
    {
        var response = await _client.PostAsJsonAsync("/auth/register", NewCustomerRegister());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResult>();
        Assert.NotEqual(Guid.Empty, body!.UserId);
        Assert.Null(body.BusinessId); // customer no tiene negocio
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }

    [Fact]
    public async Task RegisterOwner_ReturnsCreated_WithBusinessAndTokens()
    {
        var response = await _client.PostAsJsonAsync("/auth/register-owner", NewOwnerRegister());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResult>();
        Assert.NotEqual(Guid.Empty, body!.UserId);
        Assert.NotNull(body.BusinessId);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
    }

    [Fact]
    public async Task Register_WeakPassword_ReturnsBadRequest()
    {
        var weak = new RegisterCustomerRequest($"cust-{Guid.NewGuid():N}@test.local", "weak", "Ana");

        var response = await _client.PostAsJsonAsync("/auth/register", weak);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var request = NewCustomerRegister();
        (await _client.PostAsJsonAsync("/auth/register", request)).EnsureSuccessStatusCode();

        var duplicate = await _client.PostAsJsonAsync("/auth/register", request);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Login_AfterRegister_ReturnsTokens()
    {
        var request = NewCustomerRegister();
        (await _client.PostAsJsonAsync("/auth/register", request)).EnsureSuccessStatusCode();

        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(request.Email, request.Password));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResult>();
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
    }

    [Fact]
    public async Task Login_AsOwner_ReturnsBusinessId()
    {
        var request = NewOwnerRegister();
        var registered = await (await _client.PostAsJsonAsync("/auth/register-owner", request))
            .Content.ReadFromJsonAsync<AuthResult>();

        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(request.Email, request.Password));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResult>();
        Assert.Equal(registered!.BusinessId, body!.BusinessId); // el owner recupera su negocio al loguearse
    }

    [Fact]
    public async Task Login_AsCustomer_ReturnsNullBusinessId()
    {
        var request = NewCustomerRegister();
        (await _client.PostAsJsonAsync("/auth/register", request)).EnsureSuccessStatusCode();

        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest(request.Email, request.Password));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResult>();
        Assert.Null(body!.BusinessId);
    }

    [Fact]
    public async Task Refresh_AsOwner_ReturnsBusinessId()
    {
        var registered = await (await _client.PostAsJsonAsync("/auth/register-owner", NewOwnerRegister()))
            .Content.ReadFromJsonAsync<AuthResult>();

        var response = await _client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(registered!.RefreshToken));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResult>();
        Assert.Equal(registered.BusinessId, body!.BusinessId);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var request = NewCustomerRegister();
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
        var request = NewCustomerRegister();
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
        var registered = await (await _client.PostAsJsonAsync("/auth/register", NewCustomerRegister()))
            .Content.ReadFromJsonAsync<AuthResult>();

        var response = await _client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(registered!.RefreshToken));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResult>();
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.NotEqual(registered.RefreshToken, body.RefreshToken); // rotación
    }
}
