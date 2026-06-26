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
    IBusinessRepository businesses,
    IAuthRepository users)
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

        // ¿Quien hace la petición es el owner o staff del negocio? (apunta reservas desde
        // su agenda — "recepción" — y por eso puede vincularlas a la cuenta del cliente).
        var isManager = userId is { } actorId
            && (business.OwnerId == actorId || await staff.ExistsForUserAsync(actorId, business.Id, ct));

        // Negocio en 'solo calendario': no acepta reservas online. Solo el owner/staff
        // pueden apuntar reservas (desde la Agenda); un cliente/invitado, no.
        if (business.BookingMode == "calendar_only" && !isManager)
            throw new OnlineBookingDisabledException();

        // ¿Reserva para un invitado? Si llegan datos de invitado, la reserva es para un
        // cliente aunque la petición esté autenticada: así el owner/staff crea reservas
        // para clientes desde su agenda (recepción). Sin datos de invitado ni sesión, inválida.
        var bookingForGuest = !string.IsNullOrWhiteSpace(request.GuestName);
        if (!bookingForGuest && userId is null)
            throw new InvalidGuestContactException();

        // Un usuario logueado no puede reservar consigo mismo como trabajador asignado
        // (sí puede crear reservas de invitado para clientes, p. ej. el owner en su agenda).
        if (!bookingForGuest && userId is not null && worker.UserId == userId)
            throw new SelfBookingNotAllowedException();

        // Límite Freemium: nº de reservas/mes del plan (ADR #9). NULL = ilimitado.
        if (!await limits.CanAddReservationThisMonthAsync(request.BusinessId, DateTime.UtcNow, ct))
            throw new FreemiumLimitReachedException("reservas");

        var startTime = request.StartTime;
        var endTime = startTime.AddMinutes(service.DurationMinutes);

        Guid? guestId = null;
        Guid? reservationUserId = userId;
        if (bookingForGuest)
        {
            // La reserva es para un cliente, no para quien hace la petición (p. ej. el owner).
            reservationUserId = null;

            var (normalizedEmail, normalizedPhone) = NormalizeGuestContact(request);
            // Si el contacto pertenece a una cuenta registrada, solo el owner/staff puede
            // apuntar la reserva a esa cuenta (recepción). Un invitado anónimo NO puede
            // reservar "como" esa cuenta sin loguearse (evita suplantación) → debe iniciar
            // sesión. Si el contacto no tiene cuenta, se crea/reutiliza el invitado.
            var existingUser = await users.FindActiveUserByContactAsync(normalizedEmail, normalizedPhone, ct);
            if (existingUser is not null)
            {
                if (!isManager)
                    throw new ContactBelongsToAccountException();
                reservationUserId = existingUser.Id; // el owner/staff la vincula a la cuenta del cliente
            }
            else
                guestId = (await ResolveGuestAsync(request, normalizedEmail, normalizedPhone, ct)).Id;
        }

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
            UserId = reservationUserId,
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

    /// <summary>Valida el contacto del invitado (nombre + exactamente uno de teléfono/email) y lo normaliza.</summary>
    private static (string? email, string? phone) NormalizeGuestContact(CreateReservationRequest request)
    {
        var hasPhone = !string.IsNullOrWhiteSpace(request.GuestPhone);
        var hasEmail = !string.IsNullOrWhiteSpace(request.GuestEmail);

        // Exactamente uno de teléfono/email, y nombre obligatorio.
        if (string.IsNullOrWhiteSpace(request.GuestName) || hasPhone == hasEmail)
            throw new InvalidGuestContactException();

        return hasPhone
            ? (null, ContactNormalizer.NormalizePhone(request.GuestPhone!))
            : (ContactNormalizer.NormalizeEmail(request.GuestEmail!), null);
    }

    /// <summary>Busca el guest por blind index (dedupe) o lo crea cifrado, con el contacto ya normalizado.</summary>
    private async Task<Guest> ResolveGuestAsync(CreateReservationRequest request, string? normalizedEmail, string? normalizedPhone, CancellationToken ct)
    {
        string? phoneHash = null, emailHash = null;
        string? phoneEncrypted = null, emailEncrypted = null;

        if (normalizedPhone is not null)
        {
            phoneHash = blindIndex.Compute(normalizedPhone);
            phoneEncrypted = crypto.Encrypt(normalizedPhone);
        }
        else
        {
            emailHash = blindIndex.Compute(normalizedEmail!);
            emailEncrypted = crypto.Encrypt(normalizedEmail!);
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
