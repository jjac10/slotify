namespace Slotify.Domain.Entities;

/// <summary>
/// Valoración de un cliente sobre un negocio (1–5 + comentario). Una reseña por
/// (negocio, usuario): tras reservar y asistir, el cliente valora el negocio una vez
/// y puede editarla. La media/contador se denormaliza en businesses.rating /
/// review_count para que Explorar no haga joins.
/// </summary>
public class Review
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }
    public Business? Business { get; set; }

    /// <summary>Usuario que valora (debe haber tenido una reserva pasada en el negocio).</summary>
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Reserva que habilitó la reseña (la que se valoró primero).</summary>
    public Guid ReservationId { get; set; }

    /// <summary>1–5.</summary>
    public int Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Última edición de la reseña (null si nunca se editó).</summary>
    public DateTime? UpdatedAt { get; set; }
}
