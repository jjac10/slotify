using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Creación de reservas (invitado o usuario logueado). Calcula endTime según la
/// duración del servicio, deduplica/cifra el invitado y valida solapamiento.
/// La garantía dura de no solapar la da la BD (exclusion constraint, ADR #4).
/// </summary>
public class BookingService(
    IReservationRepository reservations,
    IServiceRepository services,
    IStaffRepository staff,
    IGuestRepository guests,
    ICryptoService crypto,
    IBlindIndex blindIndex,
    IFreemiumLimitService limits,
    IBusinessRepository businesses)
{
    public async Task<ReservationResponse> CreateAsync(
        CreateReservationRequest request, Guid? userId, CancellationToken ct = default)
    {
        var business = await businesses.GetByIdAsync(request.BusinessId, ct)
            ?? throw new BusinessNotFoundException(request.BusinessId);

        var service = await services.GetByIdAsync(request.ServiceId, ct);
        if (service is null || service.BusinessId != request.BusinessId)
            throw new ServiceNotFoundException(request.ServiceId);

        var worker = await staff.GetByIdAsync(request.StaffId, ct);
        if (worker is null || worker.BusinessId != request.BusinessId)
            throw new StaffNotFoundException(request.StaffId);

        // Un usuario logueado no puede reservar consigo mismo como trabajador asignado
        // (un owner/staff sí puede seguir creando reservas de invitado para clientes).
        if (userId is not null && worker.UserId == userId)
            throw new SelfBookingNotAllowedException();

        // Límite Freemium: nº de reservas/mes del plan (ADR #9). NULL = ilimitado.
        if (!await limits.CanAddReservationThisMonthAsync(request.BusinessId, DateTime.UtcNow, ct))
            throw new FreemiumLimitReachedException("reservas");

        var startTime = request.StartTime;
        var endTime = startTime.AddMinutes(service.DurationMinutes);

        Guid? guestId = null;
        if (userId is null)
            guestId = (await ResolveGuestAsync(request, ct)).Id;

        if (await reservations.HasOverlapAsync(request.StaffId, startTime, endTime, ct: ct))
            throw new SlotUnavailableException();

        // Modo de confirmación del negocio: 'auto' → la reserva nace confirmada;
        // 'manual' → nace pendiente y el owner/staff la confirma luego.
        var status = business.ConfirmationMode == "manual" ? "pending" : "confirmed";

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            BusinessId = request.BusinessId,
            ServiceId = request.ServiceId,
            StaffId = request.StaffId,
            UserId = userId,
            GuestId = guestId,
            StartTime = startTime,
            EndTime = endTime,
            Status = status,
        };
        await reservations.AddAsync(reservation, ct);

        return ReservationResponse.From(reservation);
    }

    /// <summary>Devuelve una reserva por id (null si no existe).</summary>
    public async Task<ReservationResponse?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var reservation = await reservations.GetByIdAsync(id, ct);
        return reservation is null ? null : ReservationResponse.From(reservation);
    }

    /// <summary>Busca el guest por blind index (dedupe) o lo crea cifrado.</summary>
    private async Task<Guest> ResolveGuestAsync(CreateReservationRequest request, CancellationToken ct)
    {
        var hasPhone = !string.IsNullOrWhiteSpace(request.GuestPhone);
        var hasEmail = !string.IsNullOrWhiteSpace(request.GuestEmail);

        // Exactamente uno de teléfono/email, y nombre obligatorio.
        if (string.IsNullOrWhiteSpace(request.GuestName) || hasPhone == hasEmail)
            throw new InvalidGuestContactException();

        string? phoneHash = null, emailHash = null;
        string? phoneEncrypted = null, emailEncrypted = null;

        if (hasPhone)
        {
            var normalized = ContactNormalizer.NormalizePhone(request.GuestPhone!);
            phoneHash = blindIndex.Compute(normalized);
            phoneEncrypted = crypto.Encrypt(normalized);
        }
        else
        {
            var normalized = ContactNormalizer.NormalizeEmail(request.GuestEmail!);
            emailHash = blindIndex.Compute(normalized);
            emailEncrypted = crypto.Encrypt(normalized);
        }

        var existing = await guests.FindByHashAsync(request.BusinessId, phoneHash, emailHash, ct);
        if (existing is not null)
            return existing;

        var guest = new Guest
        {
            Id = Guid.NewGuid(),
            BusinessId = request.BusinessId,
            Name = request.GuestName!,
            PhoneEncrypted = phoneEncrypted,
            EmailEncrypted = emailEncrypted,
            PhoneHash = phoneHash,
            EmailHash = emailHash,
        };
        await guests.AddAsync(guest, ct);
        return guest;
    }
}
