using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Slotify.Domain.DTOs;

namespace Slotify.Tests.Integration;

/// <summary>
/// POST /businesses/{id}/photo (multipart, campo 'photo'): guarda la imagen en el
/// almacenamiento local y deja su URL pública en photo_url; el propio backend la
/// sirve como estático en /uploads/… (→ /api/uploads/… detrás del proxy). Solo el
/// owner; tipos no imagen → 400.
/// </summary>
public class BusinessPhotoEndpointsTests : IClassFixture<BusinessPhotoEndpointsTests.UploadsFactory>, IDisposable
{
    private static readonly string UploadsRoot =
        Path.Combine(Path.GetTempPath(), $"slotify-uploads-it-{Guid.NewGuid():N}");

    private readonly UploadsFactory _factory;
    private readonly HttpClient _client;

    public BusinessPhotoEndpointsTests(UploadsFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public void Dispose()
    {
        if (Directory.Exists(UploadsRoot)) Directory.Delete(UploadsRoot, recursive: true);
    }

    private async Task<(Guid businessId, HttpClient owner)> RegisterBusinessAsync()
    {
        var req = new RegisterOwnerRequest($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", "Barbería");
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner", req)).Content.ReadFromJsonAsync<AuthResult>();
        var owner = _factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (auth.BusinessId!.Value, owner);
    }

    private static MultipartFormDataContent Photo(byte[] bytes, string contentType, string fileName = "foto.png")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "photo", fileName } };
    }

    [Fact]
    public async Task UploadPhoto_AsOwner_SetsPhotoUrl_AndServesTheFile()
    {
        var (businessId, owner) = await RegisterBusinessAsync();
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // cabecera PNG

        var res = await owner.PostAsync($"/businesses/{businessId}/photo", Photo(bytes, "image/png"));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var business = await res.Content.ReadFromJsonAsync<BusinessResponse>();
        Assert.StartsWith($"/api/uploads/businesses/{businessId}.png?v=", business!.PhotoUrl);

        // El backend sirve la foto en /uploads/… (sin el prefijo /api del proxy).
        var served = await _client.GetAsync(business.PhotoUrl!["/api".Length..]);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal(bytes, await served.Content.ReadAsByteArrayAsync());

        // Y queda persistida en el listado del owner.
        var mine = await owner.GetFromJsonAsync<List<BusinessResponse>>("/businesses");
        Assert.Equal(business.PhotoUrl, mine!.Single(b => b.Id == businessId).PhotoUrl);
    }

    [Fact]
    public async Task UploadPhoto_ReplacingPhoto_ChangesUrl_SoBrowsersRefetch()
    {
        var (businessId, owner) = await RegisterBusinessAsync();

        var first = await (await owner.PostAsync($"/businesses/{businessId}/photo", Photo([1], "image/png")))
            .Content.ReadFromJsonAsync<BusinessResponse>();
        var second = await (await owner.PostAsync($"/businesses/{businessId}/photo", Photo([2], "image/png")))
            .Content.ReadFromJsonAsync<BusinessResponse>();

        Assert.NotEqual(first!.PhotoUrl, second!.PhotoUrl); // cambia el ?v=
    }

    [Fact]
    public async Task UploadPhoto_NonImage_Returns400()
    {
        var (businessId, owner) = await RegisterBusinessAsync();

        var res = await owner.PostAsync($"/businesses/{businessId}/photo",
            Photo([1, 2], "application/pdf", "malicia.pdf"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("invalid_photo", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UploadPhoto_WithoutFile_Returns400()
    {
        var (businessId, owner) = await RegisterBusinessAsync();

        var res = await owner.PostAsync($"/businesses/{businessId}/photo", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task UploadPhoto_ByOtherOwner_Returns403()
    {
        var (businessId, _) = await RegisterBusinessAsync();
        var (_, otherOwner) = await RegisterBusinessAsync();

        var res = await otherOwner.PostAsync($"/businesses/{businessId}/photo", Photo([1], "image/png"));

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task UploadPhoto_WithoutToken_Returns401()
    {
        var (businessId, _) = await RegisterBusinessAsync();

        var res = await _client.PostAsync($"/businesses/{businessId}/photo", Photo([1], "image/png"));

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    /// <summary>Factory con el almacenamiento de fotos apuntando a un temporal del test.</summary>
    public sealed class UploadsFactory : SlotifyApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Uploads:RootPath", UploadsRoot);
        }
    }
}
