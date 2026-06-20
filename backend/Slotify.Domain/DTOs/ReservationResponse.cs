using Slotify.Domain.Entities;

namespace Slotify.Domain.DTOs;

/// <summary>
/// Representación de una reserva para la API. Los nombres (negocio/servicio/
/// trabajador) se rellenan cuando la consulta carga las navegaciones (listados);
/// en la respuesta de creación pueden ser null.
/// </summary>
public record ReservationResponse(
    Guid Id,
    Guid BusinessId,
    Guid ServiceId,
    Guid StaffId,
    Guid? UserId,
    Guid? GuestId,
    DateTime StartTime,
    DateTime EndTime,
    string Status,
    string? BusinessName = null,
    string? ServiceName = null,
    string? StaffName = null,
    string? ClientName = null)
{
    public static ReservationResponse From(Reservation r) =>
        new(r.Id, r.BusinessId, r.ServiceId, r.StaffId, r.UserId, r.GuestId, r.StartTime, r.EndTime, r.Status,
            r.Business?.Name, r.Service?.Name, r.Staff?.Name, r.Guest?.Name ?? r.User?.Name);
}
