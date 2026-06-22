using Moq;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Reseñas: un cliente registrado valora (1–5 + comentario) una reserva pasada SUYA,
/// una sola vez. Al crearla se recalcula la media/contador denormalizados del negocio.
/// </summary>
public class ReviewServiceTests
{
    private readonly Mock<IReviewRepository> _reviews = new();
    private readonly Mock<IReservationRepository> _reservations = new();
    private readonly Mock<IBusinessRepository> _businesses = new();

    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _reservationId = Guid.NewGuid();

    private static readonly DateTime Past = DateTime.UtcNow.AddDays(-2);
    private static readonly DateTime Future = DateTime.UtcNow.AddDays(2);

    private ReviewService CreateService() =>
        new(_reviews.Object, _reservations.Object, _businesses.Object);

    private void SetupReservation(Guid? userId, DateTime end)
    {
        var reservation = new Reservation
        {
            Id = _reservationId, BusinessId = _businessId, ServiceId = Guid.NewGuid(), StaffId = Guid.NewGuid(),
            UserId = userId, GuestId = userId is null ? Guid.NewGuid() : null,
            Status = "confirmed", StartTime = end.AddMinutes(-30), EndTime = end,
        };
        _reservations.Setup(r => r.GetByIdAsync(_reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = _businessId, OwnerId = Guid.NewGuid(), TierId = Guid.NewGuid(), Name = "Biz" });
        _reviews.Setup(r => r.ExistsForReservationAsync(_reservationId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _reviews.Setup(r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _reviews.Setup(r => r.GetBusinessAggregateAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync((1, (double?)5.0));
        _businesses.Setup(b => b.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task CreateAsync_ValidPastReservation_PersistsReview()
    {
        SetupReservation(_userId, Past);
        Review? saved = null;
        _reviews.Setup(r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()))
            .Callback<Review, CancellationToken>((rv, _) => saved = rv).Returns(Task.CompletedTask);

        var result = await CreateService().CreateAsync(_reservationId, _userId, 5, "Genial");

        Assert.NotNull(saved);
        Assert.Equal(_reservationId, saved!.ReservationId);
        Assert.Equal(_businessId, saved.BusinessId);
        Assert.Equal(_userId, saved.UserId);
        Assert.Equal(5, saved.Rating);
        Assert.Equal("Genial", saved.Comment);
        Assert.Equal(5, result.Rating);
    }

    [Fact]
    public async Task CreateAsync_RecomputesBusinessAggregate()
    {
        SetupReservation(_userId, Past);
        _reviews.Setup(r => r.GetBusinessAggregateAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((3, (double?)4.0));
        Business? updated = null;
        _businesses.Setup(b => b.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>()))
            .Callback<Business, CancellationToken>((b, _) => updated = b).Returns(Task.CompletedTask);

        await CreateService().CreateAsync(_reservationId, _userId, 4, null);

        Assert.NotNull(updated);
        Assert.Equal(4.0, updated!.Rating);
        Assert.Equal(3, updated.ReviewCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task CreateAsync_RatingOutOfRange_Throws(int rating)
    {
        SetupReservation(_userId, Past);
        await Assert.ThrowsAsync<InvalidReviewException>(
            () => CreateService().CreateAsync(_reservationId, _userId, rating, null));
    }

    [Fact]
    public async Task CreateAsync_ReservationNotFound_Throws()
    {
        _reservations.Setup(r => r.GetByIdAsync(_reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reservation?)null);
        await Assert.ThrowsAsync<ReservationNotFoundException>(
            () => CreateService().CreateAsync(_reservationId, _userId, 5, null));
    }

    [Fact]
    public async Task CreateAsync_NotReservationOwner_Throws403()
    {
        SetupReservation(Guid.NewGuid(), Past); // la reserva es de otro usuario
        await Assert.ThrowsAsync<ReviewForbiddenException>(
            () => CreateService().CreateAsync(_reservationId, _userId, 5, null));
    }

    [Fact]
    public async Task CreateAsync_GuestReservation_Throws403()
    {
        SetupReservation(userId: null, Past); // reserva de invitado: nadie autenticado es su dueño
        await Assert.ThrowsAsync<ReviewForbiddenException>(
            () => CreateService().CreateAsync(_reservationId, _userId, 5, null));
    }

    [Fact]
    public async Task CreateAsync_FutureReservation_ThrowsNotAllowed()
    {
        SetupReservation(_userId, Future); // aún no ha ocurrido
        await Assert.ThrowsAsync<ReviewNotAllowedException>(
            () => CreateService().CreateAsync(_reservationId, _userId, 5, null));
    }

    [Fact]
    public async Task CreateAsync_AlreadyReviewed_Throws()
    {
        SetupReservation(_userId, Past);
        _reviews.Setup(r => r.ExistsForReservationAsync(_reservationId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await Assert.ThrowsAsync<AlreadyReviewedException>(
            () => CreateService().CreateAsync(_reservationId, _userId, 5, null));
    }

    [Fact]
    public async Task ListByBusinessAsync_MapsReviews()
    {
        var review = new Review
        {
            Id = Guid.NewGuid(), BusinessId = _businessId, UserId = _userId, ReservationId = _reservationId,
            Rating = 5, Comment = "Top", CreatedAt = Past, User = new User { Id = _userId, Name = "Ana", Email = "a@a.es", PasswordHash = "x" },
        };
        _reviews.Setup(r => r.ListByBusinessAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { review });

        var list = await CreateService().ListByBusinessAsync(_businessId);

        var dto = Assert.Single(list);
        Assert.Equal(5, dto.Rating);
        Assert.Equal("Top", dto.Comment);
        Assert.Equal("Ana", dto.AuthorName);
    }
}
