using Moq;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>Renovación de tokens (refresh con rotación): válido → nuevos tokens; inválido → excepción.</summary>
public class AuthServiceRefreshTests
{
    private readonly Mock<IAuthRepository> _auth = new();
    private readonly Mock<ITierRepository> _tiers = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IRefreshTokenRepository> _refresh = new();
    private readonly Mock<IGuestRepository> _guests = new();
    private readonly Mock<IBlindIndex> _blindIndex = new();
    private readonly Mock<IBusinessRepository> _businesses = new();
    private readonly Mock<IStaffRepository> _staff = new();

    private AuthService CreateService() =>
        new(_auth.Object, _tiers.Object, _hasher.Object, _tokens.Object, _refresh.Object, _guests.Object, _blindIndex.Object, _businesses.Object, _staff.Object);

    [Fact]
    public async Task RefreshAsync_ValidToken_IssuesNewTokens()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "owner@example.com", PasswordHash = "h", Name = "Pepe", Type = "owner" };
        _refresh.Setup(r => r.ConsumeAsync("old-refresh", It.IsAny<CancellationToken>())).ReturnsAsync(user.Id);
        _auth.Setup(a => a.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _tokens.Setup(t => t.CreateAccessToken(user)).Returns("new-access");
        _tokens.Setup(t => t.CreateRefreshToken()).Returns("new-refresh");
        _refresh.Setup(r => r.IssueAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _businesses.Setup(b => b.ListByOwnerAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Business>());

        var result = await CreateService().RefreshAsync("old-refresh");

        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("new-access", result.AccessToken);
        Assert.Equal("new-refresh", result.RefreshToken);
        _refresh.Verify(r => r.IssueAsync(user.Id, "new-refresh", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshAsync_InvalidOrExpiredToken_Throws()
    {
        _refresh.Setup(r => r.ConsumeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => CreateService().RefreshAsync("nope"));

        _auth.Verify(a => a.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
