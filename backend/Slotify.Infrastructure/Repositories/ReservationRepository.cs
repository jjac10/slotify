using Microsoft.EntityFrameworkCore;
using Npgsql;
using Slotify.Domain.Entities;
using Slotify.Domain.Exceptions;
using Slotify.Domain.Interfaces;
using Slotify.Infrastructure.Data;

namespace Slotify.Infrastructure.Repositories;

/// <summary>Implementación EF Core de <see cref="IReservationRepository"/>.</summary>
public class ReservationRepository(SlotifyDbContext db) : IReservationRepository
{
    public async Task AddAsync(Reservation reservation, CancellationToken ct = default)
    {
        db.Reservations.Add(reservation);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            // El exclusion constraint (anti-doble-booking) rechazó un solape concurrente.
            db.Entry(reservation).State = EntityState.Detached;
            throw new SlotUnavailableException();
        }
    }

    public Task<bool> HasOverlapAsync(Guid staffId, DateTime start, DateTime end,
        Guid? excludeReservationId = null, CancellationToken ct = default)
        => db.Reservations.AnyAsync(r =>
            r.StaffId == staffId &&
            r.Status != "cancelled" &&
            (excludeReservationId == null || r.Id != excludeReservationId) &&
            r.StartTime < end && r.EndTime > start, ct);

    public Task<Reservation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Reservations.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task UpdateAsync(Reservation reservation, CancellationToken ct = default)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Optimistic locking: otra operación cambió la fila (version) mientras tanto.
            db.Entry(reservation).State = EntityState.Detached;
            throw new ReservationConcurrencyException();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            // El exclusion constraint (anti-doble-booking) rechazó el nuevo horario.
            db.Entry(reservation).State = EntityState.Detached;
            throw new SlotUnavailableException();
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        => await db.Reservations.Where(r => r.Id == id).ExecuteDeleteAsync(ct);

    public async Task<IReadOnlyList<Reservation>> ListByStaffOnDateAsync(Guid staffId, DateOnly date, CancellationToken ct = default)
    {
        var dayStart = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);
        return await db.Reservations.AsNoTracking()
            .Where(r => r.StaffId == staffId && r.Status != "cancelled"
                && r.StartTime >= dayStart && r.StartTime < dayEnd)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Reservation>> ListByBusinessAsync(
        Guid businessId, DateOnly? date, Guid? staffId, CancellationToken ct = default)
    {
        var query = db.Reservations.AsNoTracking()
            .Where(r => r.BusinessId == businessId && r.Status != "cancelled");

        if (staffId is not null)
            query = query.Where(r => r.StaffId == staffId);

        if (date is not null)
        {
            var dayStart = new DateTime(date.Value.Year, date.Value.Month, date.Value.Day, 0, 0, 0, DateTimeKind.Utc);
            var dayEnd = dayStart.AddDays(1);
            query = query.Where(r => r.StartTime >= dayStart && r.StartTime < dayEnd);
        }

        return await query.OrderBy(r => r.StartTime).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Reservation>> ListByUserAsync(Guid userId, CancellationToken ct = default)
        => await db.Reservations.AsNoTracking()
            .Where(r => r.UserId == userId && r.Status != "cancelled")
            .OrderBy(r => r.StartTime)
            .ToListAsync(ct);

    public Task<int> CountByBusinessAsync(Guid businessId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default)
    {
        var query = db.Reservations.AsNoTracking()
            .Where(r => r.BusinessId == businessId && r.Status != "cancelled");

        if (fromUtc is { } from)
            query = query.Where(r => r.StartTime >= from);
        if (toUtc is { } to)
            query = query.Where(r => r.StartTime < to);

        return query.CountAsync(ct);
    }

    public async Task<decimal> SumRevenueByBusinessAsync(Guid businessId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        // Join con services para tomar el precio de cada reserva; NULL (gratuito) se ignora
        // en el SUM, equivalente a 0. El resultado de SUM sobre conjunto vacío es NULL → 0.
        var total = await db.Reservations.AsNoTracking()
            .Where(r => r.BusinessId == businessId && r.Status != "cancelled"
                && r.StartTime >= fromUtc && r.StartTime < toUtc)
            .Join(db.Services, r => r.ServiceId, s => s.Id, (r, s) => s.Price)
            .SumAsync(ct);
        return total ?? 0m;
    }

    public async Task<IReadOnlyList<Reservation>> ListUpcomingByBusinessAsync(
        Guid businessId, DateTime fromUtc, int limit, CancellationToken ct = default)
        => await db.Reservations.AsNoTracking()
            .Where(r => r.BusinessId == businessId && r.Status != "cancelled" && r.StartTime >= fromUtc)
            .OrderBy(r => r.StartTime)
            .Take(limit)
            .ToListAsync(ct);
}
