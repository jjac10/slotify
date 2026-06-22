namespace Slotify.Domain.DTOs;

/// <summary>
/// Resumen del panel del propietario de un negocio:
/// <list type="bullet">
/// <item><see cref="TotalReservations"/>: reservas no canceladas (histórico).</item>
/// <item><see cref="ReservationsThisMonth"/>: reservas del mes en curso (UTC).</item>
/// <item><see cref="EstimatedMonthlyRevenue"/>: suma del precio del servicio de las
/// reservas del mes (los servicios gratuitos cuentan como 0). Es una estimación:
/// el precio puede cambiar con el tiempo.</item>
/// <item><see cref="UpcomingReservations"/>: las próximas reservas (a partir de ahora).</item>
/// <item><see cref="AverageRating"/>: media de valoraciones (1–5), null si no hay reseñas.</item>
/// <item><see cref="ReviewCount"/>: número de reseñas del negocio.</item>
/// <item><see cref="RecentReviews"/>: últimas reseñas recibidas (más recientes primero).</item>
/// </list>
/// </summary>
public record DashboardResponse(
    int TotalReservations,
    int ReservationsThisMonth,
    decimal EstimatedMonthlyRevenue,
    IReadOnlyList<ReservationResponse> UpcomingReservations,
    double? AverageRating,
    int ReviewCount,
    IReadOnlyList<ReviewResponse> RecentReviews);
