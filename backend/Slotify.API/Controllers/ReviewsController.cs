using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Slotify.Domain.DTOs;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Services;

namespace Slotify.API.Controllers;

[ApiController]
public class ReviewsController(ReviewService reviews) : ApiControllerBase
{
    /// <summary>
    /// Valora el negocio de una reserva pasada propia (1–5 + comentario). Solo el cliente
    /// registrado dueño de la reserva; una reseña por negocio (si ya existe, se edita).
    /// Recalcula la media del negocio.
    /// </summary>
    [HttpPost("/reservations/{reservationId:guid}/review")]
    [Authorize]
    public async Task<ActionResult<ReviewResponse>> Create(Guid reservationId, CreateReviewRequest request, CancellationToken ct)
    {
        try
        {
            var result = await reviews.CreateAsync(reservationId, CurrentUserId, request.Rating, request.Comment, ct);
            return CreatedAtAction(nameof(ListForBusiness), new { businessId = result.BusinessId }, result);
        }
        catch (ReservationNotFoundException ex)
        {
            return NotFound(new { error = "reservation_not_found", message = ex.Message });
        }
        catch (ReviewForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message });
        }
        catch (InvalidReviewException ex)
        {
            return BadRequest(new { error = "invalid_review", message = ex.Message });
        }
        catch (ReviewNotAllowedException ex)
        {
            return Conflict(new { error = "review_not_allowed", message = ex.Message });
        }
    }

    /// <summary>Edita una reseña propia (desde "Mis reseñas").</summary>
    [HttpPut("/reviews/{reviewId:guid}")]
    [Authorize]
    public async Task<ActionResult<MyReviewResponse>> Update(Guid reviewId, UpdateReviewRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await reviews.UpdateAsync(reviewId, CurrentUserId, request.Rating, request.Comment, ct));
        }
        catch (ReviewNotFoundException ex)
        {
            return NotFound(new { error = "review_not_found", message = ex.Message });
        }
        catch (ReviewForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden", message = ex.Message });
        }
        catch (InvalidReviewException ex)
        {
            return BadRequest(new { error = "invalid_review", message = ex.Message });
        }
    }

    /// <summary>Reseñas propias del cliente autenticado ("Mis reseñas").</summary>
    [HttpGet("/me/reviews")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<MyReviewResponse>>> ListMine(CancellationToken ct)
        => Ok(await reviews.ListMineAsync(CurrentUserId, ct));

    /// <summary>Reseñas públicas de un negocio (más recientes primero).</summary>
    [HttpGet("/businesses/{businessId:guid}/reviews")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ReviewResponse>>> ListForBusiness(Guid businessId, CancellationToken ct)
        => Ok(await reviews.ListByBusinessAsync(businessId, ct));
}
