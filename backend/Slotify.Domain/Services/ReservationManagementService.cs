using System.Text.Json;
using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Gestión del ciclo de vida de reservas (cancelar, reprogramar, confirmar). Autoriza
/// por rol: owner del negocio, staff del negocio, el propio usuario de la reserva o el
/// invitado (verificado por su teléfono/email vía blind index). Cancelar audita y luego
/// hace hard-delete; reprogramar valida solape + optimistic locking; ambos respetan la
/// ventana de antelación mínima del negocio para clientes (no para owner/staff). ADR #4/#13/#14.
/// </summary>
public class ReservationManagementService(
    IReservationRepository reservations,
    IBusinessRepository businesses,
    IStaffRepository staff,
    IAuditLogRepository audit,
    IBlindIndex blindIndex,
    IGuestRepository guests)
{
    // --- Cancelar ------------------------------------------------------------

    /// <summary>Cancela como usuario autenticado (owner, staff o el propio cliente).</summary>
    public async Task CancelAsync(Guid reservationId, Guid currentUserId, string? reason, CancellationToken ct = default)
    {
        var reservation = await reservations.GetByIdAsync(reservationId, ct)
            ?? throw new ReservationNotFoundException(reservationId);
        var business = await businesses.GetByIdAsync(reservation.BusinessId, ct);
        var actorType = ResolveActorTypeOrThrow(reservation, business, currentUserId, await IsStaffAsync(currentUserId, reservation.BusinessId, ct));

        if (IsClient(actorType))
            EnsureWithinCancellationWindow(reservation, business);

        await ApplyCancelAsync(reservation, reason, currentUserId, null, actorType, ct);
    }

    /// <summary>Cancela como invitado (sin cuenta), verificado por su teléfono/email.</summary>
    public async Task CancelAsGuestAsync(Guid reservationId, string? contact, string? reason, CancellationToken ct = default)
    {
        var reservation = await reservations.GetByIdAsync(reservationId, ct)
            ?? throw new ReservationNotFoundException(reservationId);
        await EnsureGuestOwnsReservationOrThrowAsync(reservation, contact, ct);

        var business = await businesses.GetByIdAsync(reservation.BusinessId, ct);
        EnsureWithinCancellationWindow(reservation, business);

        await ApplyCancelAsync(reservation, reason, null, reservation.GuestId, "guest", ct);
    }

    // --- Reprogramar ---------------------------------------------------------

    /// <summary>Reprograma como usuario autenticado (owner, staff o el propio cliente).</summary>
    public async Task<ReservationResponse> RescheduleAsync(
        Guid reservationId, Guid currentUserId, DateTime newStart, CancellationToken ct = default)
    {
        var reservation = await reservations.GetByIdAsync(reservationId, ct)
            ?? throw new ReservationNotFoundException(reservationId);
        var business = await businesses.GetByIdAsync(reservation.BusinessId, ct);
        var actorType = ResolveActorTypeOrThrow(reservation, business, currentUserId, await IsStaffAsync(currentUserId, reservation.BusinessId, ct));

        if (IsClient(actorType))
            EnsureWithinCancellationWindow(reservation, business);

        return await ApplyRescheduleAsync(reservation, newStart, currentUserId, null, actorType, ct);
    }

    /// <summary>Reprograma como invitado (sin cuenta), verificado por su teléfono/email.</summary>
    public async Task<ReservationResponse> RescheduleAsGuestAsync(
        Guid reservationId, string? contact, DateTime newStart, CancellationToken ct = default)
    {
        var reservation = await reservations.GetByIdAsync(reservationId, ct)
            ?? throw new ReservationNotFoundException(reservationId);
        await EnsureGuestOwnsReservationOrThrowAsync(reservation, contact, ct);

        var business = await businesses.GetByIdAsync(reservation.BusinessId, ct);
        EnsureWithinCancellationWindow(reservation, business);

        return await ApplyRescheduleAsync(reservation, newStart, null, reservation.GuestId, "guest", ct);
    }

    // --- Confirmar -----------------------------------------------------------

    /// <summary>
    /// Confirma una reserva pendiente (negocios con confirmación manual). Solo el owner
    /// del negocio o su staff (NO el cliente). Transición pending → confirmed, bump de
    /// version (optimistic locking) y auditoría action='confirmed'.
    /// </summary>
    public async Task<ReservationResponse> ConfirmAsync(
        Guid reservationId, Guid currentUserId, CancellationToken ct = default)
    {
        var reservation = await reservations.GetByIdAsync(reservationId, ct)
            ?? throw new ReservationNotFoundException(reservationId);

        // Confirmar es una acción del negocio: owner o staff, nunca el cliente.
        var actorType = await ResolveBusinessActorOrThrowAsync(reservation.BusinessId, currentUserId, ct);

        if (reservation.Status != "pending")
            throw new ReservationNotPendingException(reservation.Status);

        var oldValues = JsonSerializer.Serialize(new { reservation.Status });

        reservation.Status = "confirmed";
        reservation.Version++;
        reservation.UpdatedAt = DateTime.UtcNow;

        await reservations.UpdateAsync(reservation, ct);

        await audit.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            ReservationId = reservation.Id,
            Action = "confirmed",
            ActorId = currentUserId,
            ActorType = actorType,
            OldValues = oldValues,
            NewValues = JsonSerializer.Serialize(new { reservation.Status }),
        }, ct);

        return ReservationResponse.From(reservation);
    }

    // --- Listados ------------------------------------------------------------

    /// <summary>
    /// Agenda del negocio: reservas (no canceladas) ordenadas por inicio, con filtros
    /// opcionales por día y trabajador. Solo el owner del negocio o su staff.
    /// </summary>
    public async Task<IReadOnlyList<ReservationResponse>> ListForBusinessAsync(
        Guid businessId, Guid currentUserId, DateOnly? date, Guid? staffId, CancellationToken ct = default)
    {
        await EnsureBusinessAccessOrThrowAsync(businessId, currentUserId, ct);
        var list = await reservations.ListByBusinessAsync(businessId, date, staffId, ct);
        return list.Select(ReservationResponse.From).ToList();
    }

    /// <summary>"Mis reservas": las del usuario autenticado, ordenadas por inicio.</summary>
    public async Task<IReadOnlyList<ReservationResponse>> ListMineAsync(Guid currentUserId, CancellationToken ct = default)
    {
        var list = await reservations.ListByUserAsync(currentUserId, ct);
        return list.Select(ReservationResponse.From).ToList();
    }

    // --- Núcleo compartido ---------------------------------------------------

    private async Task ApplyCancelAsync(
        Reservation reservation, string? reason, Guid? actorId, Guid? guestId, string actorType, CancellationToken ct)
    {
        // Auditar ANTES de borrar (ADR #13/#14): el snapshot queda en old_values.
        await audit.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            ReservationId = reservation.Id,
            Action = "cancelled",
            ActorId = actorId,
            GuestId = guestId,
            ActorType = actorType,
            OldValues = JsonSerializer.Serialize(new
            {
                reservation.Status,
                reservation.StartTime,
                reservation.EndTime,
                reservation.ServiceId,
                reservation.StaffId,
            }),
            NewValues = reason is null ? null : JsonSerializer.Serialize(new { reason }),
        }, ct);

        await reservations.DeleteAsync(reservation.Id, ct);
    }

    private async Task<ReservationResponse> ApplyRescheduleAsync(
        Reservation reservation, DateTime newStart, Guid? actorId, Guid? guestId, string actorType, CancellationToken ct)
    {
        var duration = reservation.EndTime - reservation.StartTime;
        var newEnd = newStart + duration;

        var oldValues = JsonSerializer.Serialize(new { reservation.StartTime, reservation.EndTime });

        // Pre-check rápido (excluyéndose a sí misma); la garantía dura la da la BD.
        if (await reservations.HasOverlapAsync(reservation.StaffId, newStart, newEnd, reservation.Id, ct))
            throw new SlotUnavailableException();

        reservation.StartTime = newStart;
        reservation.EndTime = newEnd;
        reservation.Version++;                 // optimistic locking
        reservation.UpdatedAt = DateTime.UtcNow;

        await reservations.UpdateAsync(reservation, ct);

        await audit.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            ReservationId = reservation.Id,
            Action = "updated",
            ActorId = actorId,
            GuestId = guestId,
            ActorType = actorType,
            OldValues = oldValues,
            NewValues = JsonSerializer.Serialize(new { reservation.StartTime, reservation.EndTime }),
        }, ct);

        return ReservationResponse.From(reservation);
    }

    /// <summary>'registered_user' y 'guest' están sujetos a la ventana de antelación; owner/staff no.</summary>
    private static bool IsClient(string actorType) => actorType is "registered_user" or "guest";

    /// <summary>
    /// Lanza si el cliente intenta cancelar/reprogramar dentro de la ventana de antelación
    /// mínima del negocio (cutoff &gt; 0 y faltan menos horas que el cutoff para el inicio).
    /// </summary>
    private static void EnsureWithinCancellationWindow(Reservation reservation, Business? business)
    {
        var cutoff = business?.CancellationCutoffHours ?? 0;
        if (cutoff > 0 && reservation.StartTime - DateTime.UtcNow < TimeSpan.FromHours(cutoff))
            throw new CancellationWindowClosedException(cutoff);
    }

    private async Task<bool> IsStaffAsync(Guid userId, Guid businessId, CancellationToken ct)
        => await staff.ExistsForUserAsync(userId, businessId, ct);

    /// <summary>Devuelve el actor_type si está autorizado; si no, lanza 403.</summary>
    private static string ResolveActorTypeOrThrow(Reservation reservation, Business? business, Guid userId, bool isStaff)
    {
        if (business is not null && business.OwnerId == userId)
            return "owner";
        if (reservation.UserId == userId)
            return "registered_user";
        if (isStaff)
            return "employee";
        throw new ReservationForbiddenException();
    }

    /// <summary>
    /// Verifica que el invitado es dueño de la reserva: la reserva debe ser de invitado y
    /// el blind index del contacto aportado debe coincidir con su guest. Si no, lanza 403.
    /// </summary>
    private async Task EnsureGuestOwnsReservationOrThrowAsync(Reservation reservation, string? contact, CancellationToken ct)
    {
        if (reservation.GuestId is null || string.IsNullOrWhiteSpace(contact))
            throw new ReservationForbiddenException();

        var normalized = contact.Contains('@')
            ? ContactNormalizer.NormalizeEmail(contact)
            : ContactNormalizer.NormalizePhone(contact);
        var hash = blindIndex.Compute(normalized);
        var ids = await guests.FindIdsByContactHashAsync(hash, ct);
        if (!ids.Contains(reservation.GuestId.Value))
            throw new ReservationForbiddenException();
    }

    /// <summary>Permite el acceso a la agenda solo al owner del negocio o a su staff; si no, lanza 403.</summary>
    private async Task EnsureBusinessAccessOrThrowAsync(Guid businessId, Guid userId, CancellationToken ct)
    {
        var business = await businesses.GetByIdAsync(businessId, ct);
        if (business is not null && business.OwnerId == userId)
            return;

        if (await staff.ExistsForUserAsync(userId, businessId, ct))
            return;

        throw new ReservationForbiddenException();
    }

    /// <summary>
    /// Devuelve el actor_type ('owner'/'employee') si el usuario gestiona el negocio;
    /// si no, lanza 403. NO autoriza al cliente de la reserva (confirmar es acción del negocio).
    /// </summary>
    private async Task<string> ResolveBusinessActorOrThrowAsync(Guid businessId, Guid userId, CancellationToken ct)
    {
        var business = await businesses.GetByIdAsync(businessId, ct);
        if (business is not null && business.OwnerId == userId)
            return "owner";

        if (await staff.ExistsForUserAsync(userId, businessId, ct))
            return "employee";

        throw new ReservationForbiddenException();
    }
}
