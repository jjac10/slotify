namespace Slotify.Domain.Entities;

/// <summary>
/// Valoración de un cliente sobre una reserva pasada suya (1–5 + comentario).
/// Una reseña por reserva (unique reservation_id). La media/contador se denormaliza
/// en businesses.rating / review_count para que Explorar no haga joins.
/// </summary>
public class Review
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    /// <summary>Usuario que valora (debe ser el dueño de la reserva).</summary>
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Reserva valorada (una reseña por reserva).</summary>
    public Guid ReservationId { get; set; }

    /// <summary>1–5.</summary>
    public int Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }
}
