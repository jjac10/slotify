using Moq;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Subida de la foto del negocio: el owner sube un fichero (JPG/PNG/WebP, máx. 5 MB)
/// que se guarda vía <see cref="IPhotoStorage"/> (swappable: local en disco hoy, S3
/// mañana) y cuya URL pública queda en <c>photo_url</c>. Sustituye el "pegar URL"
/// como camino principal, que sigue funcionando.
/// </summary>
public class BusinessPhotoServiceTests
{
    private readonly Mock<IBusinessRepository> _businesses = new();
    private readonly Mock<IPhotoStorage> _storage = new();

    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    private BusinessPhotoService CreateService() => new(_businesses.Object, _storage.Object);

    private Business SetupBusiness()
    {
        var business = new Business { Id = _businessId, OwnerId = _ownerId, Name = "Barbería" };
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(business);
        return business;
    }

    private static MemoryStream SomeBytes() => new([1, 2, 3, 4]);

    [Fact]
    public async Task Upload_AsOwner_SavesPhoto_AndSetsPhotoUrl()
    {
        var business = SetupBusiness();
        _storage.Setup(s => s.SaveAsync(_businessId, "photo", It.IsAny<Stream>(), "image/png", It.IsAny<CancellationToken>()))
            .ReturnsAsync("/api/uploads/businesses/x-photo.png?v=1");

        var response = await CreateService().UploadAsync(_businessId, _ownerId, SomeBytes(), "image/png", 4);

        Assert.Equal("/api/uploads/businesses/x-photo.png?v=1", response.PhotoUrl);
        Assert.Equal("/api/uploads/businesses/x-photo.png?v=1", business.PhotoUrl);
        _businesses.Verify(b => b.UpdateAsync(business, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    [InlineData("IMAGE/PNG")] // el content type no distingue mayúsculas
    public async Task Upload_AcceptsAllowedImageTypes(string contentType)
    {
        SetupBusiness();
        _storage.Setup(s => s.SaveAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/api/uploads/businesses/x");

        var response = await CreateService().UploadAsync(_businessId, _ownerId, SomeBytes(), contentType, 4);

        Assert.NotNull(response.PhotoUrl);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/svg+xml")] // los SVG pueden llevar scripts: fuera
    [InlineData("text/html")]
    [InlineData(null)]
    public async Task Upload_RejectsNonImageTypes_WithoutTouchingStorage(string? contentType)
    {
        SetupBusiness();

        await Assert.ThrowsAsync<InvalidPhotoException>(() =>
            CreateService().UploadAsync(_businessId, _ownerId, SomeBytes(), contentType!, 4));

        _storage.Verify(s => s.SaveAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Upload_TooBig_Throws_WithoutTouchingStorage()
    {
        SetupBusiness();

        await Assert.ThrowsAsync<InvalidPhotoException>(() =>
            CreateService().UploadAsync(_businessId, _ownerId, SomeBytes(), "image/png", BusinessPhotoService.MaxBytes + 1));

        _storage.Verify(s => s.SaveAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Upload_NotTheOwner_Throws()
    {
        SetupBusiness();

        await Assert.ThrowsAsync<NotBusinessOwnerException>(() =>
            CreateService().UploadAsync(_businessId, Guid.NewGuid(), SomeBytes(), "image/png", 4));
    }

    [Fact]
    public async Task Upload_UnknownBusiness_Throws()
    {
        await Assert.ThrowsAsync<BusinessNotFoundException>(() =>
            CreateService().UploadAsync(Guid.NewGuid(), _ownerId, SomeBytes(), "image/png", 4));
    }
}
