using Moq;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Resumen del panel del owner: agrega contadores + ingresos del mes + próximas
/// reservas. Autorización owner-only (404 si no existe, 403 si no es el dueño).
/// </summary>
public class DashboardServiceTests
{
    private readonly Mock<IReservationRepository> _reservations = new();
    private readonly Mock<IBusinessRepository> _businesses = new();
    private readonly Mock<IReviewRepository> _reviews = new();

    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    private DashboardService CreateService() => new(_reservations.Object, _businesses.Object, _reviews.Object);

    private void SetupBusinessOwnedBy(Guid ownerId, double? rating = null, int reviewCount = 0)
        => _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = _businessId, OwnerId = ownerId, Name = "Barbería", Rating = rating, ReviewCount = reviewCount });

    [Fact]
    public async Task GetAsync_ForOwner_AggregatesMetricsAndMapsUpcoming()
    {
        SetupBusinessOwnedBy(_ownerId, rating: 4.5, reviewCount: 10);
        _reviews.Setup(r => r.ListByBusinessAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Review>
            {
                new() { Id = Guid.NewGuid(), BusinessId = _businessId, UserId = Guid.NewGuid(), ReservationId = Guid.NewGuid(), Rating = 5, Comment = "Top", CreatedAt = DateTime.UtcNow },
            });
        var now = new DateTime(2026, 6, 17, 10, 0, 0, DateTimeKind.Utc);
        var monthStart = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        _reservations.Setup(r => r.CountByBusinessAsync(_businessId, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);
        _reservations.Setup(r => r.CountByBusinessAsync(_businessId, monthStart, monthEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);
        _reservations.Setup(r => r.SumRevenueByBusinessAsync(_businessId, monthStart, monthEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync(175m);
        var upcomingId = Guid.NewGuid();
        _reservations.Setup(r => r.ListUpcomingByBusinessAsync(_businessId, now, DashboardService.UpcomingLimit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Reservation>
            {
                new()
                {
                    Id = upcomingId, BusinessId = _businessId, ServiceId = Guid.NewGuid(), StaffId = Guid.NewGuid(),
                    StartTime = now.AddHours(2), EndTime = now.AddHours(2).AddMinutes(30), Status = "pending",
                },
            });

        var result = await CreateService().GetAsync(_businessId, _ownerId, now);

        Assert.Equal(42, result.TotalReservations);
        Assert.Equal(7, result.ReservationsThisMonth);
        Assert.Equal(175m, result.EstimatedMonthlyRevenue);
        Assert.Single(result.UpcomingReservations);
        Assert.Equal(upcomingId, result.UpcomingReservations[0].Id);
        Assert.Equal(4.5, result.AverageRating);
        Assert.Equal(10, result.ReviewCount);
        Assert.Single(result.RecentReviews);
        Assert.Equal(5, result.RecentReviews[0].Rating);
    }

    [Fact]
    public async Task GetAsync_WhenBusinessDoesNotExist_ThrowsBusinessNotFound()
    {
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Business?)null);

        await Assert.ThrowsAsync<BusinessNotFoundException>(
            () => CreateService().GetAsync(_businessId, _ownerId, DateTime.UtcNow));
    }

    [Fact]
    public async Task GetAsync_WhenCallerIsNotOwner_ThrowsNotBusinessOwner()
    {
        SetupBusinessOwnedBy(Guid.NewGuid()); // pertenece a otro

        await Assert.ThrowsAsync<NotBusinessOwnerException>(
            () => CreateService().GetAsync(_businessId, _ownerId, DateTime.UtcNow));
    }
}
