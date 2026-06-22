using Moq;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Domain.Services;

namespace Slotify.Tests.Unit;

/// <summary>
/// Reseñas: un cliente registrado valora un negocio (1–5 + comentario) tras una reserva
/// pasada suya — una reseña por (negocio, usuario), editable. Al crear/editar se recalcula
/// la media/contador denormalizados del negocio.
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
        _reviews.Setup(r => r.GetByBusinessAndUserAsync(_businessId, _userId, It.IsAny<CancellationToken>())).ReturnsAsync((Review?)null);
        _reviews.Setup(r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _reviews.Setup(r => r.UpdateAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
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
    public async Task CreateAsync_WhenBusinessAlreadyReviewed_EditsExistingInsteadOfAdding()
    {
        SetupReservation(_userId, Past);
        var existing = new Review
        {
            Id = Guid.NewGuid(), BusinessId = _businessId, UserId = _userId, ReservationId = Guid.NewGuid(),
            Rating = 3, Comment = "Regular", CreatedAt = Past,
        };
        _reviews.Setup(r => r.GetByBusinessAndUserAsync(_businessId, _userId, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await CreateService().CreateAsync(_reservationId, _userId, 5, "Mejoró");

        Assert.Equal(5, existing.Rating);
        Assert.Equal("Mejoró", existing.Comment);
        Assert.NotNull(existing.UpdatedAt);
        Assert.Equal(5, result.Rating);
        _reviews.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        _reviews.Verify(r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()), Times.Never);
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

    // --- Editar (PUT /reviews/{id}) ---

    private Review SetupExistingReview(Guid ownerId)
    {
        var review = new Review
        {
            Id = Guid.NewGuid(), BusinessId = _businessId, UserId = ownerId, ReservationId = _reservationId,
            Rating = 3, Comment = "Regular", CreatedAt = Past, Business = new Business { Id = _businessId, Name = "Biz", OwnerId = Guid.NewGuid(), TierId = Guid.NewGuid() },
        };
        _reviews.Setup(r => r.GetByIdAsync(review.Id, It.IsAny<CancellationToken>())).ReturnsAsync(review);
        _reviews.Setup(r => r.UpdateAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _businesses.Setup(b => b.GetByIdAsync(_businessId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = _businessId, OwnerId = Guid.NewGuid(), TierId = Guid.NewGuid(), Name = "Biz" });
        _reviews.Setup(r => r.GetBusinessAggregateAsync(_businessId, It.IsAny<CancellationToken>())).ReturnsAsync((1, (double?)5.0));
        _businesses.Setup(b => b.UpdateAsync(It.IsAny<Business>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return review;
    }

    [Fact]
    public async Task UpdateAsync_AsAuthor_EditsAndRecomputes()
    {
        var review = SetupExistingReview(_userId);

        var result = await CreateService().UpdateAsync(review.Id, _userId, 5, "Mucho mejor");

        Assert.Equal(5, review.Rating);
        Assert.Equal("Mucho mejor", review.Comment);
        Assert.NotNull(review.UpdatedAt);
        Assert.Equal(5, result.Rating);
        _reviews.Verify(r => r.UpdateAsync(review, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_NotAuthor_Throws403()
    {
        var review = SetupExistingReview(Guid.NewGuid()); // la reseña es de otro usuario
        await Assert.ThrowsAsync<ReviewForbiddenException>(
            () => CreateService().UpdateAsync(review.Id, _userId, 5, null));
    }

    [Fact]
    public async Task UpdateAsync_NotFound_Throws()
    {
        var reviewId = Guid.NewGuid();
        _reviews.Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>())).ReturnsAsync((Review?)null);
        await Assert.ThrowsAsync<ReviewNotFoundException>(
            () => CreateService().UpdateAsync(reviewId, _userId, 5, null));
    }

    [Fact]
    public async Task ListMineAsync_MapsWithBusinessName()
    {
        var review = new Review
        {
            Id = Guid.NewGuid(), BusinessId = _businessId, UserId = _userId, ReservationId = _reservationId,
            Rating = 4, Comment = "Bien", CreatedAt = Past, Business = new Business { Id = _businessId, Name = "Barbería X", OwnerId = Guid.NewGuid(), TierId = Guid.NewGuid() },
        };
        _reviews.Setup(r => r.ListByUserAsync(_userId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { review });

        var list = await CreateService().ListMineAsync(_userId);

        var dto = Assert.Single(list);
        Assert.Equal("Barbería X", dto.BusinessName);
        Assert.Equal(4, dto.Rating);
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
