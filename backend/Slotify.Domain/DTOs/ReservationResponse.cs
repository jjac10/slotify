using Slotify.Domain.Entities;

namespace Slotify.Domain.DTOs;

/// <summary>Representación de una reserva para la API.</summary>
public record ReservationResponse(
    Guid Id,
    Guid BusinessId,
    Guid ServiceId,
    Guid StaffId,
    Guid? UserId,
    Guid? GuestId,
    DateTime StartTime,
    DateTime EndTime,
    string Status)
{
    public static ReservationResponse From(Reservation r) =>
        new(r.Id, r.BusinessId, r.ServiceId, r.StaffId, r.UserId, r.GuestId, r.StartTime, r.EndTime, r.Status);
}
