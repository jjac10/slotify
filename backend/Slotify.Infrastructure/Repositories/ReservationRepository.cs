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

    public Task<bool> HasOverlapAsync(Guid staffId, DateTime start, DateTime end, CancellationToken ct = default)
        => db.Reservations.AnyAsync(r =>
            r.StaffId == staffId &&
            r.Status != "cancelled" &&
            r.StartTime < end && r.EndTime > start, ct);

    public Task<Reservation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Reservations.FirstOrDefaultAsync(r => r.Id == id, ct);
}
