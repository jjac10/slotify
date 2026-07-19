using System.Security.Cryptography;
using System.Text;
using Moq;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Pago del plan Premium (cierra el TODO "gatear tras pago real"): el upgrade entra
/// SIEMPRE por el checkout de la pasarela (IPaymentGateway swappable: Stripe real o
/// simulado) — abrir checkout crea una suscripción 'pending' y completarlo la activa
/// y sube el plan. El downgrade a Free sigue siendo directo y cancela la suscripción.
/// PUT /plan con 'premium' queda bloqueado (409 payment_required).
/// </summary>
public class SubscriptionServiceTests
{
    private readonly Mock<ISubscriptionRepository> _subs = new();
    private readonly Mock<IPaymentGateway> _gateway = new();
    private readonly Mock<IBusinessRepository> _businesses = new();
    private readonly Mock<ITierRepository> _tiers = new();

    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _freeTierId = Guid.NewGuid();
    private readonly Guid _premiumTierId = Guid.NewGuid();

    public SubscriptionServiceTests()
    {
        _tiers.Setup(t => t.GetByCodeAsync("premium", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingTier { Id = _premiumTierId, Code = "premium", Name = "Premium" });
        _tiers.Setup(t => t.GetByCodeAsync("free", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PricingTier { Id = _freeTierId, Code = "free", Name = "Free" });
        _gateway.SetupGet(g => g.Provider).Returns("simulated");
    }

    private BusinessService CreateBusinessService() => new(_businesses.Object, _tiers.Object);

    private SubscriptionService CreateService() => new(
        _subs.Object, _gateway.Object, _businesses.Object, _tiers.Object, CreateBusinessService());

    private Business SetupBusiness(Guid? tierId = null)
    {
        var business = new Business
        {
            Id = _businessId, OwnerId = _ownerId, TierId = tierId ?? _freeTierId, Name = "Barbería",
        };
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(business);
        return business;
    }

    // --- Abrir checkout ---------------------------------------------------------

    [Fact]
    public async Task StartCheckout_AsOwnerOnFreePlan_CreatesPendingSubscription_AndReturnsUrl()
    {
        SetupBusiness();
        _gateway.Setup(g => g.CreateCheckoutAsync(_businessId, "Barbería", It.IsAny<CancellationToken>()))
            .ReturnsAsync(("tok-123", "/api/checkout/simulated/tok-123"));
        Subscription? added = null;
        _subs.Setup(s => s.AddAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()))
            .Callback<Subscription, CancellationToken>((s, _) => added = s).Returns(Task.CompletedTask);

        var url = await CreateService().StartCheckoutAsync(_businessId, _ownerId);

        Assert.Equal("/api/checkout/simulated/tok-123", url);
        Assert.NotNull(added);
        Assert.Equal("pending", added!.Status);
        Assert.Equal("simulated", added.Provider);
        Assert.Equal("tok-123", added.ExternalId);
        Assert.Equal(_businessId, added.BusinessId);
    }

    [Fact]
    public async Task StartCheckout_AlreadyPremium_Throws_AndDoesNotCallGateway()
    {
        SetupBusiness(tierId: _premiumTierId);

        await Assert.ThrowsAsync<AlreadyPremiumException>(() =>
            CreateService().StartCheckoutAsync(_businessId, _ownerId));

        _gateway.Verify(g => g.CreateCheckoutAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartCheckout_NotTheOwner_Throws()
    {
        SetupBusiness();

        await Assert.ThrowsAsync<NotBusinessOwnerException>(() =>
            CreateService().StartCheckoutAsync(_businessId, Guid.NewGuid()));
    }

    // --- Completar checkout (webhook / simulado) ---------------------------------

    [Fact]
    public async Task CompleteCheckout_PendingSubscription_ActivatesIt_AndUpgradesPlan()
    {
        var business = SetupBusiness();
        var sub = new Subscription
        {
            Id = Guid.NewGuid(), BusinessId = _businessId, Provider = "simulated",
            ExternalId = "tok-123", Status = "pending", CreatedAt = DateTime.UtcNow,
        };
        _subs.Setup(s => s.GetByExternalIdAsync("tok-123", It.IsAny<CancellationToken>())).ReturnsAsync(sub);

        await CreateService().CompleteCheckoutAsync("tok-123");

        Assert.Equal("active", sub.Status);
        Assert.NotNull(sub.ActivatedAt);
        _subs.Verify(s => s.UpdateAsync(sub, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(_premiumTierId, business.TierId); // el plan subió a premium
    }

    [Fact]
    public async Task CompleteCheckout_AlreadyActive_IsIdempotent()
    {
        SetupBusiness(tierId: _premiumTierId);
        var sub = new Subscription
        {
            Id = Guid.NewGuid(), BusinessId = _businessId, Provider = "stripe",
            ExternalId = "cs_1", Status = "active", CreatedAt = DateTime.UtcNow, ActivatedAt = DateTime.UtcNow,
        };
        _subs.Setup(s => s.GetByExternalIdAsync("cs_1", It.IsAny<CancellationToken>())).ReturnsAsync(sub);

        await CreateService().CompleteCheckoutAsync("cs_1"); // reintento del webhook: sin error

        _subs.Verify(s => s.UpdateAsync(It.IsAny<Subscription>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteCheckout_UnknownToken_Throws()
    {
        await Assert.ThrowsAsync<CheckoutNotFoundException>(() =>
            CreateService().CompleteCheckoutAsync("no-existe"));
    }

    // --- Downgrade ---------------------------------------------------------------

    [Fact]
    public async Task Downgrade_SetsFreePlan_AndCancelsActiveSubscription()
    {
        var business = SetupBusiness(tierId: _premiumTierId);
        var sub = new Subscription
        {
            Id = Guid.NewGuid(), BusinessId = _businessId, Provider = "simulated",
            ExternalId = "tok-9", Status = "active", CreatedAt = DateTime.UtcNow,
        };
        _subs.Setup(s => s.GetActiveByBusinessAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync(sub);

        await CreateService().DowngradeAsync(_businessId, _ownerId);

        Assert.Equal(_freeTierId, business.TierId);
        Assert.Equal("cancelled", sub.Status);
        Assert.NotNull(sub.CancelledAt);
    }
}

/// <summary>El upgrade directo por PUT /plan queda gateado tras el pago.</summary>
public class ChangePlanPaymentGateTests
{
    [Fact]
    public async Task ChangePlanAsync_ToPremium_ThrowsPaymentRequired()
    {
        var businessId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var businesses = new Mock<IBusinessRepository>();
        businesses.Setup(b => b.GetByIdAsync(businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = businessId, OwnerId = ownerId, TierId = Guid.NewGuid(), Name = "Biz" });

        var service = new BusinessService(businesses.Object, Mock.Of<ITierRepository>());

        await Assert.ThrowsAsync<PaymentRequiredException>(() =>
            service.ChangePlanAsync(businessId, ownerId, "premium"));
    }
}

/// <summary>Verificación de la firma del webhook de Stripe (cabecera Stripe-Signature).</summary>
public class StripeWebhookVerifierTests
{
    private const string Secret = "whsec_test_secret";
    private const string Payload = """{"type":"checkout.session.completed"}""";

    private static string Sign(string payload, long timestamp)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return Convert.ToHexStringLower(hash);
    }

    [Fact]
    public void Verify_ValidSignature_ReturnsTrue()
    {
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = $"t={t},v1={Sign(Payload, t)}";

        Assert.True(StripeWebhookVerifier.Verify(Payload, header, Secret, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Verify_TamperedPayload_ReturnsFalse()
    {
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = $"t={t},v1={Sign(Payload, t)}";

        Assert.False(StripeWebhookVerifier.Verify("""{"type":"otro"}""", header, Secret, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Verify_TooOldTimestamp_ReturnsFalse_ReplayProtection()
    {
        var old = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        var header = $"t={old},v1={Sign(Payload, old)}";

        Assert.False(StripeWebhookVerifier.Verify(Payload, header, Secret, DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("t=abc,v1=def")]
    public void Verify_MalformedHeader_ReturnsFalse(string header)
    {
        Assert.False(StripeWebhookVerifier.Verify(Payload, header, Secret, DateTimeOffset.UtcNow));
    }
}
