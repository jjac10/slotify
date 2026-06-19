using System.Text.Json;
using Slotify.Domain.DTOs;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;

namespace Slotify.Domain.Services;

/// <summary>
/// Gestión del ciclo de vida de reservas (cancelar, reprogramar). Autoriza por rol:
/// owner del negocio, staff del negocio o el propio usuario de la reserva. Cancelar
/// audita y luego hace hard-delete; reprogramar valida solape + optimistic locking y
/// audita action='updated' (ADR #4/#13/#14).
/// </summary>
public class ReservationManagementService(
    IReservationRepository reservations,
    IBusinessRepository businesses,
    IStaffRepository staff,
    IAuditLogRepository audit)
{
    public async Task CancelAsync(Guid reservationId, Guid currentUserId, string? reason, CancellationToken ct = default)
    {
        var reservation = await reservations.GetByIdAsync(reservationId, ct)
            ?? throw new ReservationNotFoundException(reservationId);

        var actorType = await ResolveActorTypeOrThrowAsync(reservation, currentUserId, ct);

        // Auditar ANTES de borrar (ADR #13/#14): el snapshot queda en old_values.
        await audit.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            ReservationId = reservation.Id,
            Action = "cancelled",
            ActorId = currentUserId,
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

    /// <summary>
    /// Reprograma una reserva a un nuevo inicio conservando la duración. Valida
    /// autorización por rol, solape (excluyéndose a sí misma; la BD lo garantiza con
    /// el exclusion constraint) y optimistic locking (version). Audita action='updated'.
    /// </summary>
    public async Task<ReservationResponse> RescheduleAsync(
        Guid reservationId, Guid currentUserId, DateTime newStart, CancellationToken ct = default)
    {
        var reservation = await reservations.GetByIdAsync(reservationId, ct)
            ?? throw new ReservationNotFoundException(reservationId);

        var actorType = await ResolveActorTypeOrThrowAsync(reservation, currentUserId, ct);

        var duration = reservation.EndTime - reservation.StartTime;
        var newEnd = newStart + duration;

        // Snapshot del horario anterior para la auditoría.
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
            ActorId = currentUserId,
            ActorType = actorType,
            OldValues = oldValues,
            NewValues = JsonSerializer.Serialize(new { reservation.StartTime, reservation.EndTime }),
        }, ct);

        return ReservationResponse.From(reservation);
    }

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

    /// <summary>Devuelve el actor_type si está autorizado; si no, lanza 403.</summary>
    private async Task<string> ResolveActorTypeOrThrowAsync(Reservation reservation, Guid userId, CancellationToken ct)
    {
        var business = await businesses.GetByIdAsync(reservation.BusinessId, ct);
        if (business is not null && business.OwnerId == userId)
            return "owner";

        if (reservation.UserId == userId)
            return "registered_user";

        if (await staff.ExistsForUserAsync(userId, reservation.BusinessId, ct))
            return "employee";

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
    /// si no, lanza 403. A diferencia de <see cref="ResolveActorTypeOrThrowAsync"/>, NO
    /// autoriza al cliente de la reserva (confirmar es acción del negocio).
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
