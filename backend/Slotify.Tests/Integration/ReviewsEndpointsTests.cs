using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Infrastructure.Data;

namespace Slotify.Tests.Integration;

/// <summary>
/// Reseñas end-to-end: un cliente registrado valora una reserva pasada suya
/// (POST /reservations/{id}/review), aparece en GET /businesses/{id}/reviews y la
/// media se refleja en el negocio (rating/reviewCount). Autorización y reglas (una
/// sola vez, solo pasadas, solo el dueño) por código HTTP.
/// </summary>
public class ReviewsEndpointsTests(SlotifyApiFactory factory) : IClassFixture<SlotifyApiFactory>
{
    private readonly SlotifyApiFactory _factory = factory;
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<(Guid businessId, string businessName, Guid serviceId, Guid staffId)> SetupBusinessAsync()
    {
        var name = $"Barberia {Guid.NewGuid():N}";
        var req = new RegisterOwnerRequest($"owner-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Pepe", name);
        var auth = await (await _client.PostAsJsonAsync("/auth/register-owner", req)).Content.ReadFromJsonAsync<AuthResult>();
        var businessId = auth!.BusinessId!.Value;
        var owner = _factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var service = await (await owner.PostAsJsonAsync($"/businesses/{businessId}/services",
            new CreateServiceRequest("Corte", null, 30, 15m, null))).Content.ReadFromJsonAsync<ServiceResponse>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        var staffId = await db.Staff.Where(s => s.BusinessId == businessId).Select(s => s.Id).FirstAsync();
        return (businessId, name, service!.Id, staffId);
    }

    private async Task<(Guid userId, HttpClient client)> RegisterCustomerAsync()
    {
        var req = new RegisterCustomerRequest($"cust-{Guid.NewGuid():N}@test.local", "SecurePass123!", "Ana");
        var auth = await (await _client.PostAsJsonAsync("/auth/register", req)).Content.ReadFromJsonAsync<AuthResult>();
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (auth.UserId, c);
    }

    // Hora distinta por reserva sembrada para no chocar con el anti-doble-booking del staff.
    private static int _seq;

    /// <summary>Inserta una reserva directamente (saltando validación de slot) en el pasado o futuro.</summary>
    private async Task<Guid> SeedReservationAsync(Guid businessId, Guid serviceId, Guid staffId, Guid userId, bool past)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SlotifyDbContext>();
        var slot = Interlocked.Increment(ref _seq);
        var start = (past ? DateTime.UtcNow.AddDays(-2) : DateTime.UtcNow.AddDays(2)).AddHours(slot);
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(), BusinessId = businessId, ServiceId = serviceId, StaffId = staffId,
            UserId = userId, Status = "confirmed", StartTime = start, EndTime = start.AddMinutes(30),
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();
        return reservation.Id;
    }

    private async Task<double?> GetPublicRatingAsync(string businessName, Guid businessId)
    {
        var list = await (await _client.GetAsync($"/public/businesses?q={Uri.EscapeDataString(businessName)}"))
            .Content.ReadFromJsonAsync<List<BusinessResponse>>();
        return list!.First(b => b.Id == businessId).Rating;
    }

    [Fact]
    public async Task Customer_ReviewsPastReservation_AppearsAndUpdatesBusinessRating()
    {
        var (businessId, name, serviceId, staffId) = await SetupBusinessAsync();
        var (userId, customer) = await RegisterCustomerAsync();
        var reservationId = await SeedReservationAsync(businessId, serviceId, staffId, userId, past: true);

        var res = await customer.PostAsJsonAsync($"/reservations/{reservationId}/review", new CreateReviewRequest(5, "Genial"));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var reviews = await (await _client.GetAsync($"/businesses/{businessId}/reviews"))
            .Content.ReadFromJsonAsync<List<ReviewResponse>>();
        var review = Assert.Single(reviews!);
        Assert.Equal(5, review.Rating);
        Assert.Equal("Genial", review.Comment);
        Assert.Equal("Ana", review.AuthorName);

        Assert.Equal(5.0, await GetPublicRatingAsync(name, businessId));
    }

    [Fact]
    public async Task TwoReviews_AverageRating()
    {
        var (businessId, name, serviceId, staffId) = await SetupBusinessAsync();
        var (user1, c1) = await RegisterCustomerAsync();
        var (user2, c2) = await RegisterCustomerAsync();
        var r1 = await SeedReservationAsync(businessId, serviceId, staffId, user1, past: true);
        var r2 = await SeedReservationAsync(businessId, serviceId, staffId, user2, past: true);

        Assert.Equal(HttpStatusCode.Created, (await c1.PostAsJsonAsync($"/reservations/{r1}/review", new CreateReviewRequest(5, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await c2.PostAsJsonAsync($"/reservations/{r2}/review", new CreateReviewRequest(3, null))).StatusCode);

        Assert.Equal(4.0, await GetPublicRatingAsync(name, businessId));
    }

    [Fact]
    public async Task Review_Again_EditsExisting_OneReviewPerBusiness()
    {
        var (businessId, name, serviceId, staffId) = await SetupBusinessAsync();
        var (userId, customer) = await RegisterCustomerAsync();
        // Dos reservas pasadas del mismo cliente en el mismo negocio.
        var r1 = await SeedReservationAsync(businessId, serviceId, staffId, userId, past: true);
        var r2 = await SeedReservationAsync(businessId, serviceId, staffId, userId, past: true);

        Assert.Equal(HttpStatusCode.Created, (await customer.PostAsJsonAsync($"/reservations/{r1}/review", new CreateReviewRequest(4, "ok"))).StatusCode);
        // Volver a valorar (otra reserva del mismo negocio) edita la misma reseña, no crea otra.
        Assert.Equal(HttpStatusCode.Created, (await customer.PostAsJsonAsync($"/reservations/{r2}/review", new CreateReviewRequest(2, "peor"))).StatusCode);

        var reviews = await (await _client.GetAsync($"/businesses/{businessId}/reviews"))
            .Content.ReadFromJsonAsync<List<ReviewResponse>>();
        var review = Assert.Single(reviews!); // sigue habiendo una sola reseña
        Assert.Equal(2, review.Rating);
        Assert.Equal("peor", review.Comment);
        Assert.Equal(2.0, await GetPublicRatingAsync(name, businessId));
    }

    [Fact]
    public async Task EditReview_ViaMeReviews_UpdatesRatingAndAverage()
    {
        var (businessId, name, serviceId, staffId) = await SetupBusinessAsync();
        var (userId, customer) = await RegisterCustomerAsync();
        var reservationId = await SeedReservationAsync(businessId, serviceId, staffId, userId, past: true);
        await customer.PostAsJsonAsync($"/reservations/{reservationId}/review", new CreateReviewRequest(5, "genial"));

        // "Mis reseñas" devuelve la propia, con el nombre del negocio.
        var mine = await (await customer.GetAsync("/me/reviews")).Content.ReadFromJsonAsync<List<MyReviewResponse>>();
        var review = Assert.Single(mine!);
        Assert.Equal(name, review.BusinessName);
        Assert.Equal(5, review.Rating);

        // Editar la reseña baja la media del negocio.
        var edit = await customer.PutAsJsonAsync($"/reviews/{review.Id}", new UpdateReviewRequest(2, "lo pensé mejor"));
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(2.0, await GetPublicRatingAsync(name, businessId));
    }

    [Fact]
    public async Task EditReview_ByOtherUser_Returns403()
    {
        var (businessId, _, serviceId, staffId) = await SetupBusinessAsync();
        var (userId, customer) = await RegisterCustomerAsync();
        var (_, other) = await RegisterCustomerAsync();
        var reservationId = await SeedReservationAsync(businessId, serviceId, staffId, userId, past: true);
        var created = await (await customer.PostAsJsonAsync($"/reservations/{reservationId}/review", new CreateReviewRequest(5, null)))
            .Content.ReadFromJsonAsync<ReviewResponse>();

        var res = await other.PutAsJsonAsync($"/reviews/{created!.Id}", new UpdateReviewRequest(1, null));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Review_ByOtherUser_Returns403()
    {
        var (businessId, _, serviceId, staffId) = await SetupBusinessAsync();
        var (ownerUserId, _) = await RegisterCustomerAsync();
        var (_, other) = await RegisterCustomerAsync();
        var reservationId = await SeedReservationAsync(businessId, serviceId, staffId, ownerUserId, past: true);

        var res = await other.PostAsJsonAsync($"/reservations/{reservationId}/review", new CreateReviewRequest(5, null));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Review_FutureReservation_Returns409()
    {
        var (businessId, _, serviceId, staffId) = await SetupBusinessAsync();
        var (userId, customer) = await RegisterCustomerAsync();
        var reservationId = await SeedReservationAsync(businessId, serviceId, staffId, userId, past: false);

        var res = await customer.PostAsJsonAsync($"/reservations/{reservationId}/review", new CreateReviewRequest(5, null));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Review_WithoutToken_Returns401()
    {
        var (businessId, _, serviceId, staffId) = await SetupBusinessAsync();
        var (userId, _) = await RegisterCustomerAsync();
        var reservationId = await SeedReservationAsync(businessId, serviceId, staffId, userId, past: true);

        var res = await _client.PostAsJsonAsync($"/reservations/{reservationId}/review", new CreateReviewRequest(5, null));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Review_RatingOutOfRange_Returns400()
    {
        var (businessId, _, serviceId, staffId) = await SetupBusinessAsync();
        var (userId, customer) = await RegisterCustomerAsync();
        var reservationId = await SeedReservationAsync(businessId, serviceId, staffId, userId, past: true);

        var res = await customer.PostAsJsonAsync($"/reservations/{reservationId}/review", new CreateReviewRequest(6, null));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
