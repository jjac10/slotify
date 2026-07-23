using Moq;
using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Marca propia del negocio: logo (subido como la foto, slot aparte) y color de
/// marca (#RRGGBB) editable en el perfil. Ambos se lucen en la ficha pública.
/// </summary>
public class BusinessBrandingTests
{
    private readonly Mock<IBusinessRepository> _businesses = new();
    private readonly Mock<ITierRepository> _tiers = new();
    private readonly Mock<IPhotoStorage> _storage = new();

    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    private Business SetupBusiness()
    {
        var business = new Business { Id = _businessId, OwnerId = _ownerId, TierId = Guid.NewGuid(), Name = "Barbería" };
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(business);
        return business;
    }

    private BusinessService CreateBusinessService() => new(_businesses.Object, _tiers.Object);

    private BusinessPhotoService CreatePhotoService() => new(_businesses.Object, _storage.Object);

    private static UpdateBusinessProfileRequest ProfileWith(string? brandColor = null, string? logoUrl = null) =>
        new(null, null, null, null, BrandColor: brandColor, LogoUrl: logoUrl);

    // --- Color de marca -----------------------------------------------------

    [Theory]
    [InlineData("#7C3AED", "#7c3aed")] // se normaliza a minúsculas
    [InlineData("#06b6d4", "#06b6d4")]
    public async Task UpdateProfile_ValidBrandColor_Persists(string input, string stored)
    {
        var business = SetupBusiness();

        var result = await CreateBusinessService().UpdateProfileAsync(_businessId, _ownerId, ProfileWith(brandColor: input));

        Assert.Equal(stored, business.BrandColor);
        Assert.Equal(stored, result.BrandColor);
    }

    [Theory]
    [InlineData("rojo")]
    [InlineData("#7C3AE")]      // 5 dígitos
    [InlineData("#7C3AED99")]   // 8 dígitos (nada de alfa)
    [InlineData("#GGGGGG")]
    [InlineData("7C3AED")]      // sin '#'
    [InlineData("javascript:x")]
    public async Task UpdateProfile_InvalidBrandColor_Throws(string brandColor)
    {
        SetupBusiness();

        await Assert.ThrowsAsync<InvalidBusinessProfileException>(() =>
            CreateBusinessService().UpdateProfileAsync(_businessId, _ownerId, ProfileWith(brandColor: brandColor)));
    }

    [Fact]
    public async Task UpdateProfile_BlankBrandColor_ClearsIt()
    {
        var business = SetupBusiness();
        business.BrandColor = "#7c3aed";

        await CreateBusinessService().UpdateProfileAsync(_businessId, _ownerId, ProfileWith(brandColor: "  "));

        Assert.Null(business.BrandColor);
    }

    // --- Logo (perfil y subida) ----------------------------------------------

    [Fact]
    public async Task UpdateProfile_PersistsLogoUrl_AndBlankClearsIt()
    {
        var business = SetupBusiness();

        await CreateBusinessService().UpdateProfileAsync(_businessId, _ownerId, ProfileWith(logoUrl: "/api/uploads/businesses/x-logo.png"));
        Assert.Equal("/api/uploads/businesses/x-logo.png", business.LogoUrl);

        await CreateBusinessService().UpdateProfileAsync(_businessId, _ownerId, ProfileWith(logoUrl: null));
        Assert.Null(business.LogoUrl);
    }

    [Fact]
    public async Task UploadLogo_SavesInLogoSlot_AndSetsLogoUrl_NotPhotoUrl()
    {
        var business = SetupBusiness();
        business.PhotoUrl = "/api/uploads/businesses/x-photo.jpg";
        _storage.Setup(s => s.SaveAsync(_businessId, "logo", It.IsAny<Stream>(), "image/png", It.IsAny<CancellationToken>()))
            .ReturnsAsync("/api/uploads/businesses/x-logo.png?v=1");

        var response = await CreatePhotoService().UploadAsync(
            _businessId, _ownerId, new MemoryStream([1]), "image/png", 1, BusinessImageKind.Logo);

        Assert.Equal("/api/uploads/businesses/x-logo.png?v=1", business.LogoUrl);
        Assert.Equal("/api/uploads/businesses/x-photo.jpg", business.PhotoUrl); // la foto no se toca
        Assert.Equal("/api/uploads/businesses/x-logo.png?v=1", response.LogoUrl);
        _businesses.Verify(b => b.UpdateAsync(business, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UploadLogo_InvalidType_Throws()
    {
        SetupBusiness();

        await Assert.ThrowsAsync<InvalidPhotoException>(() => CreatePhotoService().UploadAsync(
            _businessId, _ownerId, new MemoryStream([1]), "image/svg+xml", 1, BusinessImageKind.Logo));
    }
}
